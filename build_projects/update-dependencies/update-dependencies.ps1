#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [switch]$Help,
    [switch]$Update)

if($Help)
{
    Write-Output "Usage: .\update-dependencies.ps1"
    Write-Output ""
    Write-Output "Options:"
    Write-Output "  -Help                 Display this help message"
    Write-Output "  -Update               Update dependencies (but don't open a PR)"
    exit 0
}

$Architecture='x64'

$RepoRoot = "$PSScriptRoot\..\.."
$ProjectPath = "$PSScriptRoot\update-dependencies.csproj"
$ProjectArgs = ""

if ($Update)
{
    $ProjectArgs = "--Update"
}

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
if (!$env:DOTNET_INSTALL_DIR)
{
    $env:DOTNET_INSTALL_DIR="$RepoRoot\.dotnet_stage0\$Architecture"
}

# Install a stage 0
Write-Output "Installing .NET Core CLI Stage 0"
& "$RepoRoot\scripts\obtain\dotnet-install.ps1" -Channel "master" -Architecture $Architecture
if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }

# Put the stage0 on the path
$env:PATH = "$env:DOTNET_INSTALL_DIR;$env:PATH"

# Restore the app
Write-Output "Restoring $ProjectPath..."
dotnet restore "$ProjectPath"
if($LASTEXITCODE -ne 0) { throw "Failed to restore" }

# Run the app
Write-Output "Invoking App $ProjectPath..."
dotnet run -p "$ProjectPath" "$ProjectArgs"
if($LASTEXITCODE -ne 0) { throw "Build failed" }
