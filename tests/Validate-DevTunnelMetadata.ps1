$ErrorActionPreference = 'Stop'

Import-Module "$PSScriptRoot\..\tools\DevTunnel.psm1" -Force

$preHostJson = Get-Content "$PSScriptRoot\devtunnel-show-wrapped.json" -Raw
$preHost = Get-PulseTunnelConfiguration `
    -Json $preHostJson `
    -TunnelId 'pulse-student-test' `
    -Port 7004

if ($null -ne $preHost.PortUri) {
    throw 'Pre-host devtunnel metadata unexpectedly required a portUri.'
}

$preHostWithExpectedUrl = Get-PulseTunnelConfiguration `
    -Json $preHostJson `
    -TunnelId 'pulse-student-test' `
    -Port 7004 `
    -PublicJoinBaseUrl 'https://pulse-student-test.example.devtunnels.ms'
if ($null -ne $preHostWithExpectedUrl.PortUri) {
    throw 'Pre-host metadata with an expected URL unexpectedly returned a portUri.'
}

$hostedJson = Get-Content "$PSScriptRoot\devtunnel-show-hosted.json" -Raw
$hosted = Get-PulseTunnelConfiguration `
    -Json $hostedJson `
    -TunnelId 'pulse-student-test' `
    -Port 7004

if ($hosted.PortUri -ne 'https://pulse-student-test.example.devtunnels.ms') {
    throw 'Hosted wrapped devtunnel JSON did not return the expected portUri.'
}

$legacy = ($hostedJson | ConvertFrom-Json).tunnel
$legacy.accessControl = [pscustomobject]@{
    entries = @(
        [pscustomobject]@{
            subjects = @( 'Anonymous' )
            scopes = @( 'connect' )
        }
    )
}
$legacyJson = $legacy | ConvertTo-Json -Depth 10
$legacy = Get-PulseTunnelConfiguration `
    -Json $legacyJson `
    -TunnelId 'pulse-student-test' `
    -Port 7004 `
    -PublicJoinBaseUrl 'https://pulse-student-test.example.devtunnels.ms'

if ($legacy.PortUri -ne $hosted.PortUri) {
    throw 'Legacy top-level devtunnel JSON was not parsed consistently.'
}

if (-not (Test-PulseHealthContent -Content 'Healthy')) {
    throw 'The repository health response was not accepted.'
}
if (Test-PulseHealthContent -Content '<html>devtunnel anti-phishing</html>') {
    throw 'An HTTP 200 anti-phishing page was accepted as healthy.'
}
if (Test-PulseHealthContent -Content "Healthy`n") {
    throw 'Health response validation was not exact.'
}
$healthHeaders = Get-PulsePublicHealthHeaders
if ($healthHeaders['X-Tunnel-Skip-AntiPhishing-Page'] -ne 'true' -or
    $healthHeaders.Accept -ne 'text/plain') {
    throw 'Public health probe headers do not bypass the tunnel interstitial.'
}

$whitespace = Get-PulseTunnelConfiguration `
    -Json $hostedJson `
    -TunnelId 'pulse-student-test' `
    -Port 7004 `
    -PublicJoinBaseUrl '  https://pulse-student-test.example.devtunnels.ms/  '
if ($whitespace.PortUri -ne $hosted.PortUri) {
    throw 'Whitespace-padded public URL was not normalized.'
}

$withoutAnonymous = $hostedJson.Replace('"Anonymous"', '"Developers"')
try {
    Get-PulseTunnelConfiguration `
        -Json $withoutAnonymous `
        -TunnelId 'pulse-student-test' `
        -Port 7004 `
        -PublicJoinBaseUrl 'https://pulse-student-test.example.devtunnels.ms'
    throw 'Metadata without Anonymous connect access was accepted.'
}
catch {
    if ($_.Exception.Message -notlike '*Anonymous connect*') {
        throw
    }
}

$wrongUrl = 'https://another-tunnel.example.devtunnels.ms'
try {
    Get-PulseTunnelConfiguration `
        -Json $hostedJson `
        -TunnelId 'pulse-student-test' `
        -Port 7004 `
        -PublicJoinBaseUrl $wrongUrl
    throw 'Mismatched public URL was accepted.'
}
catch {
    if ($_.Exception.Message -notlike '*does not match*') {
        throw
    }
}

Write-Host 'Dev tunnel metadata fixture validation passed.'
