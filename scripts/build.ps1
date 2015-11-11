param(
    [string]$Configuration="Debug")

$ErrorActionPreference="Stop"

if (!$env:DOTNET_BUILD_VERSION) {
    # Get the timestamp of the most recent commit
    $timestamp = git log -1 --format=%ct
    $origin = New-Object -Type DateTime -ArgumentList 1970, 1, 1, 0, 0, 0, 0
    $commitTime = $origin.AddSeconds($timestamp)
    $LastCommitTimestamp = $commitTime.ToString("yyyyMMdd-HHmmss")

    $env:DOTNET_BUILD_VERSION = "0.0.1-alpha-$LastCommitTimestamp"
}

Write-Host -ForegroundColor Green "*** Building dotnet tools version $($env:DOTNET_BUILD_VERSION) - $Configuration ***"
& "$PSScriptRoot\compile.ps1" -Configuration:$Configuration

Write-Host -ForegroundColor Green "*** Packaging dotnet ***"
& "$PSScriptRoot\package\package.ps1"
