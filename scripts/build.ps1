#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$Configuration="Debug")

. "$PSScriptRoot\_common.ps1"

$ErrorActionPreference="Stop"

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
$env:DOTNET_INSTALL_DIR="$(Convert-Path "$PSScriptRoot\..")\.dotnet_stage0\win7-x64"
if (!(Test-Path $env:DOTNET_INSTALL_DIR))
{
    mkdir $env:DOTNET_INSTALL_DIR | Out-Null
}
$env:PATH = "$env:DOTNET_INSTALL_DIR\cli\bin;$env:PATH"

if (!$env:DOTNET_BUILD_VERSION) {
    # Get the timestamp of the most recent commit
    $timestamp = git log -1 --format=%ct
    $commitTime = [timespan]::FromSeconds($timestamp)

    $majorVersion = 1
    $minorVersion = 0
    $buildnumber = $commitTime.Days
    $revnumber = $commitTime.TotalSeconds

    $env:DOTNET_BUILD_VERSION = "$majorVersion.$minorVersion.$buildnumber.$revnumber"
}

Write-Host -ForegroundColor Green "*** Building dotnet tools version $($env:DOTNET_BUILD_VERSION) - $Configuration ***"
& "$PSScriptRoot\compile.ps1" -Configuration:$Configuration
if (!$?) {
    Write-Host "Building dotnet tools finished with errors."
    Exit 1
}

Write-Host -ForegroundColor Green "*** Packaging dotnet ***"
& "$PSScriptRoot\package\package.ps1"
if (!$?) {
    Write-Host "Packaging dotnet finished with errors."
    Exit 1
}


Write-Host -ForegroundColor Green "*** Generating dotnet MSI ***"
& "$RepoRoot\packaging\windows\generatemsi.ps1" $Stage2Dir
if (!$?) {
    Write-Host "Generating dotnet MSI finished with errors."
    Exit 1
}
