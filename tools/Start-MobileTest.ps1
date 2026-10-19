[CmdletBinding()]
param(
    [string] $PublicJoinBaseUrl,

    [Parameter(Mandatory)]
    [string] $TunnelId
)

$ErrorActionPreference = 'Stop'
$studentPort = 7004
$localHealthUrl = "https://localhost:$studentPort/health"
Import-Module "$PSScriptRoot\DevTunnel.psm1" -Force

if (-not (Get-Command devtunnel -ErrorAction SilentlyContinue)) {
    throw 'devtunnel was not found. Install it with: winget install Microsoft.devtunnel'
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'dotnet was not found. Install the .NET 10 SDK.'
}

function Test-TcpPort {
    param([string] $HostName, [int] $Port)

    $connection = [Net.Sockets.TcpClient]::new()
    try {
        $connection.Connect($HostName, $Port)
        return $true
    }
    catch {
        return $false
    }
    finally {
        $connection.Dispose()
    }
}

function Wait-ForHttp {
    param(
        [string] $Url,
        [Diagnostics.Process] $Owner,
        [int] $TimeoutSeconds,
        [hashtable] $Headers = @{},
        [string] $ExpectedContent,
        [switch] $ShowCertificateHelp
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $lastFailure = 'No response was received.'
    do {
        if ($Owner.HasExited) {
            throw "Process $($Owner.Id) exited before $Url became ready (exit code $($Owner.ExitCode))."
        }

        try {
            $requestParameters = @{
                Uri = $Url
                TimeoutSec = 5
                UseBasicParsing = $true
                ErrorAction = 'Stop'
            }
            if ($Headers.Count -gt 0) {
                $requestParameters.Headers = $Headers
            }

            $response = Invoke-WebRequest @requestParameters
            if ($response.StatusCode -eq 200 -and
                ([string]::IsNullOrEmpty($ExpectedContent) -or
                 (Test-PulseHealthContent -Content $response.Content))) {
                return
            }

            $lastFailure = "HTTP $($response.StatusCode) returned unexpected content."
        }
        catch {
            $lastFailure = $_.Exception.Message
            if ($ShowCertificateHelp -and
                $lastFailure -match 'certificate|SSL|authentication') {
                throw "$Url failed TLS certificate validation. Run 'dotnet dev-certs https --trust', restart Aspire, and retry. Error: $lastFailure"
            }
        }
        Start-Sleep -Seconds 1
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "$Url did not return HTTP 200 with content '$ExpectedContent' within $TimeoutSeconds seconds. Last error: $lastFailure"
}

function Stop-TrackedProcess {
    param([Diagnostics.Process] $Process, [string] $Label)

    if ($Process.HasExited) {
        return
    }

    Write-Host "Stopping $Label process $($Process.Id)..."
    if ($Process.CloseMainWindow()) {
        $Process.WaitForExit(5000) | Out-Null
    }
    if (-not $Process.HasExited) {
        Stop-Process -Id $Process.Id
        $Process.WaitForExit()
    }
}

if (Test-TcpPort -HostName 'localhost' -Port $studentPort) {
    throw "Port $studentPort is already in use. Stop the existing process before launching this stack."
}

$tunnelJson = (& devtunnel show $TunnelId --json | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw "Named tunnel '$TunnelId' was not found. Create it before running this script."
}
Get-PulseTunnelConfiguration `
    -Json $tunnelJson `
    -TunnelId $TunnelId `
    -Port $studentPort | Out-Null

$previousPublicUrl = $env:Pulse__PublicJoinBaseUrl
$appHost = $null
$tunnelHost = $null

try {
    Write-Host "Hosting named tunnel '$TunnelId'."
    $tunnelHost = Start-Process devtunnel `
        -ArgumentList @('host', $TunnelId) `
        -NoNewWindow `
        -PassThru

    $metadataDeadline = [DateTime]::UtcNow.AddSeconds(30)
    $tunnelConfiguration = $null
    do {
        if ($tunnelHost.HasExited) {
            throw "devtunnel exited before publishing its URL (exit code $($tunnelHost.ExitCode))."
        }

        $hostedTunnelJson = (& devtunnel show $TunnelId --json | Out-String)
        if ($LASTEXITCODE -eq 0) {
            $tunnelConfiguration = Get-PulseTunnelConfiguration `
                -Json $hostedTunnelJson `
                -TunnelId $TunnelId `
                -Port $studentPort `
                -PublicJoinBaseUrl $PublicJoinBaseUrl
        }
        if ($null -eq $tunnelConfiguration.PortUri) {
            Start-Sleep -Seconds 1
        }
    } while ($null -eq $tunnelConfiguration.PortUri -and [DateTime]::UtcNow -lt $metadataDeadline)

    if ($null -eq $tunnelConfiguration.PortUri) {
        throw "Named tunnel '$TunnelId' did not publish portUri within 30 seconds."
    }

    $publicBaseUrl = $tunnelConfiguration.PortUri
    $env:Pulse__PublicJoinBaseUrl = $publicBaseUrl

    Write-Host "Starting Aspire with discovered public student join base URL: $publicBaseUrl"
    $appHost = Start-Process dotnet `
        -ArgumentList @('run', '--project', 'src\Pulse.AppHost\Pulse.AppHost.csproj') `
        -NoNewWindow `
        -PassThru

    Wait-ForHttp `
        -Url $localHealthUrl `
        -Owner $appHost `
        -TimeoutSeconds 90 `
        -ExpectedContent 'Healthy' `
        -ShowCertificateHelp
    Wait-ForHttp `
        -Url "$publicBaseUrl/health" `
        -Owner $tunnelHost `
        -TimeoutSeconds 45 `
        -Headers (Get-PulsePublicHealthHeaders) `
        -ExpectedContent 'Healthy'

    Write-Host "Student endpoint: https://localhost:$studentPort"
    Write-Host "Verify a generated QR opens: $publicBaseUrl/join/<JOIN_CODE>"
    Write-Host 'Anonymous public HTTPS access is ready.'
    Write-Host 'Press Ctrl+C to stop the tunnel and Aspire.'
    Wait-Process -Id $tunnelHost.Id
    $tunnelHost.Refresh()
    if ($tunnelHost.ExitCode -ne 0) {
        throw "devtunnel exited with code $($tunnelHost.ExitCode)."
    }
}
finally {
    if ($null -ne $tunnelHost) {
        Stop-TrackedProcess -Process $tunnelHost -Label 'devtunnel'
    }
    if ($null -ne $appHost) {
        Stop-TrackedProcess -Process $appHost -Label 'Aspire'
    }

    $releaseDeadline = [DateTime]::UtcNow.AddSeconds(15)
    while ((Test-TcpPort -HostName 'localhost' -Port $studentPort) -and
           [DateTime]::UtcNow -lt $releaseDeadline) {
        Start-Sleep -Milliseconds 500
    }
    if (Test-TcpPort -HostName 'localhost' -Port $studentPort) {
        Write-Warning "Port $studentPort is still in use; inspect the Aspire shutdown before restarting."
    }

    $env:Pulse__PublicJoinBaseUrl = $previousPublicUrl
}
