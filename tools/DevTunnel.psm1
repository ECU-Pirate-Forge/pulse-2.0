function Get-PulseTunnelConfiguration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $Json,

        [Parameter(Mandatory)]
        [string] $TunnelId,

        [Parameter(Mandatory)]
        [int] $Port,

        [string] $PublicJoinBaseUrl
    )

    $parsed = $Json | ConvertFrom-Json
    $metadata = if ($null -ne $parsed.tunnel) { $parsed.tunnel } else { $parsed }

    if ($null -eq $metadata) {
        throw 'devtunnel returned no tunnel metadata.'
    }
    if ($metadata.tunnelId -and $metadata.tunnelId -ne $TunnelId) {
        throw "devtunnel returned metadata for '$($metadata.tunnelId)' instead of '$TunnelId'."
    }

    $tunnelPort = @($metadata.ports) |
        Where-Object { $_.portNumber -eq $Port } |
        Select-Object -First 1
    if ($null -eq $tunnelPort -or $tunnelPort.protocol -ne 'https') {
        throw "Named tunnel '$TunnelId' must contain port $Port with protocol https."
    }
    $publishedUrl = $null
    if (-not [string]::IsNullOrWhiteSpace($tunnelPort.portUri)) {
        $publishedUrl = $tunnelPort.portUri.Trim().TrimEnd('/')
        $publishedUri = $null
        if (-not [Uri]::TryCreate($publishedUrl, [UriKind]::Absolute, [ref]$publishedUri) -or
            $publishedUri.Scheme -ne 'https' -or
            -not [string]::IsNullOrEmpty($publishedUri.UserInfo) -or
            -not [string]::IsNullOrEmpty($publishedUri.Query) -or
            -not [string]::IsNullOrEmpty($publishedUri.Fragment)) {
            throw "Named tunnel '$TunnelId' port $Port returned an unsafe portUri."
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($publishedUrl) -and
        -not [string]::IsNullOrWhiteSpace($PublicJoinBaseUrl) -and
        $PublicJoinBaseUrl.Trim().TrimEnd('/') -ne $publishedUrl) {
        throw "PublicJoinBaseUrl does not match the public URL for named tunnel '$TunnelId' port $Port."
    }

    $accessEntries = @()
    foreach ($accessControl in @($metadata.accessControl, $tunnelPort.accessControl)) {
        if ($null -eq $accessControl) {
            continue
        }
        if ($null -ne $accessControl.entries) {
            $accessEntries += @($accessControl.entries)
        }
        else {
            $accessEntries += @($accessControl)
        }
    }
    $allowsAnonymousConnect = $accessEntries | Where-Object {
        ($_.type -eq 'Anonymous' -or @($_.subjects) -contains 'Anonymous') -and
        @($_.scopes) -contains 'connect'
    }
    if (-not $allowsAnonymousConnect) {
        throw "Named tunnel '$TunnelId' must grant Anonymous connect access."
    }

    [pscustomobject]@{
        TunnelId = $TunnelId
        Port = $Port
        PortUri = $publishedUrl
    }
}

function Test-PulseHealthContent {
    [CmdletBinding()]
    param(
        [AllowEmptyString()]
        [string] $Content
    )

    return $Content -ceq 'Healthy'
}

function Get-PulsePublicHealthHeaders {
    return @{
        'X-Tunnel-Skip-AntiPhishing-Page' = 'true'
        Accept = 'text/plain'
    }
}

Export-ModuleMember -Function `
    Get-PulseTunnelConfiguration, `
    Test-PulseHealthContent, `
    Get-PulsePublicHealthHeaders
