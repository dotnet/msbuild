#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string[]]$Targets=@("Default"),
    [switch]$Help)

if($Help)
{
    Write-Host "Usage: .\update-dependencies.ps1 [-Targets <TARGETS...>]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Targets <TARGETS...>              Comma separated build targets to run (UpdateFiles, PushPR; Default is everything)"
    Write-Host "  -Help                              Display this help message"
    exit 0
}

$Architecture='x64'

$RepoRoot = "$PSScriptRoot\..\.."
$AppPath = "$PSScriptRoot"

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
if (!$env:DOTNET_INSTALL_DIR)
{
    $env:DOTNET_INSTALL_DIR="$RepoRoot\.dotnet_stage0\Windows\$Architecture"
}

# Install a stage 0
Write-Host "Installing .NET Core CLI Stage 0"
& "$RepoRoot\scripts\obtain\dotnet-install.ps1" -Architecture $Architecture
if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }

# Put the stage0 on the path
$env:PATH = "$env:DOTNET_INSTALL_DIR;$env:PATH"

# Restore the build_projects
Write-Host "Restoring Build projects..."
pushd "$RepoRoot\build_projects"
dotnet restore
if($LASTEXITCODE -ne 0) { throw "Failed to restore" }
popd

# Publish the app
Write-Host "Compiling App..."
dotnet publish "$AppPath" -o "$AppPath\bin" --framework netcoreapp1.0
if($LASTEXITCODE -ne 0) { throw "Failed to compile build scripts" }

# Run the app
Write-Host "Invoking App $AppPath..."
Write-Host " Configuration: $env:CONFIGURATION"
& "$AppPath\bin\update-dependencies.exe" @Targets
if($LASTEXITCODE -ne 0) { throw "Build failed" }
