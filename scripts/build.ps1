#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$Configuration="Debug")

$ErrorActionPreference="Stop"

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
$env:DOTNET_INSTALL_DIR="$PSScriptRoot\.dotnet_stage0\win7-x64"
if (!(Test-Path $env:DOTNET_INSTALL_DIR))
{
    mkdir $env:DOTNET_INSTALL_DIR | Out-Null
}
$env:PATH = "$env:DOTNET_INSTALL_DIR\cli\bin;$env:PATH"

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
