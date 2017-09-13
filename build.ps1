#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$Configuration="Debug",
    [string]$Platform="Any CPU",
    [string]$Verbosity="minimal",
    [switch]$SkipTests,
    [switch]$FullMSBuild,
    [switch]$RealSign,
    [switch]$Help,
    [Parameter(Position=0, ValueFromRemainingArguments=$true)]
    $ExtraParameters)

if($Help)
{
    Write-Host "Usage: .\build.ps1 [Options]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Configuration <CONFIGURATION>     Build the specified Configuration (Debug or Release, default: Debug)"
    Write-Host "  -Platform <PLATFORM>               Build the specified Platform (Any CPU)"
    Write-Host "  -Verbosity <VERBOSITY>             Build console output verbosity (minimal or diagnostic, default: minimal)"
    Write-Host "  -SkipTests                         Skip executing unit tests"
    Write-Host "  -FullMSBuild                       Run tests with the full .NET Framework version of MSBuild instead of the .NET Core version"
    Write-Host "  -RealSign                          Sign the output DLLs"
    Write-Host "  -Help                              Display this help message"
    Write-Host ""
    Write-Host "Any additional parameters will be passed through to the main invocation of MSBuild"
    exit 0
}

$RepoRoot = "$PSScriptRoot"
$PackagesPath = "$RepoRoot\packages"
$env:NUGET_PACKAGES = $PackagesPath
$DotnetCLIVersion = Get-Content "$RepoRoot\DotnetCLIVersion.txt"

# Use a repo-local install directory (but not the bin directory because that gets cleaned a lot)
if (!$env:DOTNET_INSTALL_DIR)
{
    $env:DOTNET_INSTALL_DIR="$RepoRoot\.dotnet_cli\"
}

if (!(Test-Path $env:DOTNET_INSTALL_DIR))
{
    mkdir $env:DOTNET_INSTALL_DIR | Out-Null
}

if ($Verbosity -eq 'diagnostic') {
    $dotnetInstallVerbosity = "-Verbose"
}

# Install a stage 0
$DOTNET_INSTALL_SCRIPT_URL="https://dot.net/v1/dotnet-install.ps1"
Invoke-WebRequest $DOTNET_INSTALL_SCRIPT_URL -OutFile "$env:DOTNET_INSTALL_DIR\dotnet-install.ps1"

& "$env:DOTNET_INSTALL_DIR\dotnet-install.ps1" -Version $DotnetCLIVersion $dotnetInstallVerbosity
if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }

# This is a hack to prevent this target from being imported twice. We want to import the one that is build in the repo. If anyone knows
# of a better way to do this, let licavalc known and I will immediatelly fix it.
$NETBuildExtensionsTargets = "$env:DOTNET_INSTALL_DIR\sdk\$DotnetCLIVersion\15.0\Microsoft.Common.targets\ImportAfter\Microsoft.NET.Build.Extensions.targets"
if (Test-Path $NETBuildExtensionsTargets)
{
    Remove-Item $NETBuildExtensionsTargets
}

# Install 1.0.4 shared framework
if (!(Test-Path "$env:DOTNET_INSTALL_DIR\shared\Microsoft.NETCore.App\1.0.5"))
{
    & "$env:DOTNET_INSTALL_DIR\dotnet-install.ps1" -Version 1.0.5 -SharedRuntime
    if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }
}

# Install 1.1.1 shared framework
if (!(Test-Path "$env:DOTNET_INSTALL_DIR\shared\Microsoft.NETCore.App\1.1.2"))
{
    & "$env:DOTNET_INSTALL_DIR\dotnet-install.ps1" -Version 1.1.2 -SharedRuntime
    if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }
}

# Download nuget.exe
if (!(Test-Path "$RepoRoot\.nuget"))
{
    mkdir "$RepoRoot\.nuget" | Out-Null
    $NUGET_EXE_URL="https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
    Invoke-WebRequest $NUGET_EXE_URL -OutFile "$RepoRoot\.nuget\nuget.exe"
}

# Put the stage0 on the path
$env:PATH = "$env:DOTNET_INSTALL_DIR;$env:PATH"

# Disable first run since we want to control all package sources
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Don't resolve runtime, shared framework, or SDK from other locations
$env:DOTNET_MULTILEVEL_LOOKUP=0

$logPath = "$RepoRoot\bin\log"
if (!(Test-Path -Path $logPath)) {
    New-Item -Path $logPath -Force -ItemType 'Directory' | Out-Null
}

$signType = 'public'
if ($RealSign) {
    $signType = 'real'
}

$buildTarget = 'Build'
if ($SkipTests) {
    $buildTarget = 'BuildWithoutTesting'
}

if ($FullMSBuild)
{
    $env:DOTNET_SDK_TEST_MSBUILD_PATH = join-path $env:VSInstallDir "MSBuild\15.0\bin\MSBuild.exe"
}

$commonBuildArgs = echo $RepoRoot\build\build.proj /m:1 /nologo /p:Configuration=$Configuration /p:Platform=$Platform /p:SignType=$signType /verbosity:$Verbosity /warnaserror

# NET Core Build 
$msbuildSummaryLog = Join-Path -path $logPath -childPath "sdk.log"
$msbuildWarningLog = Join-Path -path $logPath -childPath "sdk.wrn"
$msbuildFailureLog = Join-Path -path $logPath -childPath "sdk.err"
$msbuildBinLog = Join-Path -path $logPath -childPath "sdk.binlog"

dotnet msbuild /t:$buildTarget $commonBuildArgs /flp1:Summary`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildSummaryLog /flp2:WarningsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildWarningLog /flp3:ErrorsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$msbuildFailureLog /bl:$msbuildBinLog $ExtraParameters
if($LASTEXITCODE -ne 0) { throw "Failed to build" }


