#!/usr/bin/env pwsh
# Test and coverage script for Pulse

param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

Write-Host "Running .NET tests with coverage..."

# Run tests with coverage
dotnet test Pulse.slnx `
    --configuration $Configuration `
    --logger "trx;LogFileName=TestResults.trx" `
    --collect:"XPlat Code Coverage" `
    --results-directory "src/Pulse.Tests/TestResults"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Tests completed successfully!"
