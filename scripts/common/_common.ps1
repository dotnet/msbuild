#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. $PSScriptRoot\_utility.ps1

$Rid = "win7-x64"
$Tfm = "dnxcore50"
$RepoRoot = Resolve-Path "$PSScriptRoot\..\.."
$OutputDir = "$RepoRoot\artifacts\$Rid"
$Stage1Dir = "$OutputDir\stage1"
$Stage1CompilationDir = "$OutputDir\stage1compilation"
$Stage2Dir = "$OutputDir\stage2"
$Stage2CompilationDir = "$OutputDir\stage2compilation"
$HostDir = "$OutputDir\corehost"
$PackageDir = "$RepoRoot\artifacts\packages\dnvm"
$TestBinRoot = "$RepoRoot\artifacts\tests"
$TestPackageDir = "$TestBinRoot\packages"

$env:TEST_ROOT = "$OutputDir\tests"  
$env:TEST_ARTIFACTS = "$env:TEST_ROOT\artifacts"  

$env:ReleaseSuffix = "beta"
$env:Channel = "$env:ReleaseSuffix"

# Set reasonable defaults for unset variables
setEnvIfDefault "DOTNET_INSTALL_DIR"  "$(Convert-Path "$PSScriptRoot\..")\.dotnet_stage0\win7-x64"
setEnvIfDefault "DOTNET_CLI_VERSION" "0.1.0.0"
setPathAndHomeIfDefault "$Stage2Dir"
setVarIfDefault "Configuration" "Debug"

# Common Files which depend on above properties
. $PSScriptRoot\_nuget.ps1
. $PSScriptRoot\_configuration.ps1