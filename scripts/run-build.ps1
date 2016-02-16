#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$Configuration="Debug",
    [switch]$NoPackage,
    [switch]$Help)

if($Help)
{
    Write-Host "Usage: .\build.cmd [-Configuration <CONFIGURATION>] [-NoPackage] [-Help] <TARGETS...>"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Configuration <CONFIGURATION>     Build the specified Configuration (Debug or Release, default: Debug)"
    Write-Host "  -NoPackage                         Skip packaging targets"
    Write-Host "  -Help                              Display this help message"
    Write-Host "  <TARGETS...>                       The build targets to run (Init, Compile, Publish, etc.; Default is a full build and publish)"
    exit 0
}

$env:CONFIGURATION = $Configuration;

if($NoPackage)
{
    $env:DOTNET_BUILD_SKIP_PACKAGING=1
}
else
{
    $env:DOTNET_BUILD_SKIP_PACKAGING=0
}

# Load Branch Info
cat "$PSScriptRoot\..\branchinfo.txt" | ForEach-Object {
    if(!$_.StartsWith("#") -and ![String]::IsNullOrWhiteSpace($_)) {
        $splat = $_.Split([char[]]@("="), 2)
        Set-Content "env:\$($splat[0])" -Value $splat[1]
    }
}

$env:CHANNEL=$env:RELEASE_SUFFIX

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
if (!$env:DOTNET_INSTALL_DIR)
{
    $env:DOTNET_INSTALL_DIR="$PSScriptRoot\..\.dotnet_stage0\Windows"
}

if (!(Test-Path $env:DOTNET_INSTALL_DIR))
{
    mkdir $env:DOTNET_INSTALL_DIR | Out-Null
}

# Install a stage 0
Write-Host "Installing .NET Core CLI Stage 0 from beta channel"
& "$PSScriptRoot\obtain\install.ps1" -Channel $env:CHANNEL

# Put the stage0 on the path
$env:PATH = "$env:DOTNET_INSTALL_DIR\cli\bin;$env:PATH"

# Restore the build scripts
Write-Host "Restoring Build Script projects..."
pushd $PSScriptRoot
dotnet restore
if($LASTEXITCODE -ne 0) { throw "Failed to restore" }
popd

# Publish the builder
Write-Host "Compiling Build Scripts..."
dotnet publish "$PSScriptRoot\dotnet-cli-build" -o "$PSScriptRoot/dotnet-cli-build/bin" --framework dnxcore50
if($LASTEXITCODE -ne 0) { throw "Failed to compile build scripts" }

# Run the builder
Write-Host "Invoking Build Scripts..."
Write-Host " Configuration: $env:CONFIGURATION"
$env:DOTNET_HOME="$env:DOTNET_INSTALL_DIR\cli"
& "$PSScriptRoot\dotnet-cli-build\bin\dotnet-cli-build.exe" @args
if($LASTEXITCODE -ne 0) { throw "Build failed" }
