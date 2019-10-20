#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$Configuration="Debug",
    [string]$Architecture="x64",
    [Parameter(ValueFromRemainingArguments=$true)][String[]]$ExtraParameters
)

$env:CONFIGURATION = $Configuration;
$RepoRoot = "$PSScriptRoot"
if(!$env:NUGET_PACKAGES){
  $env:NUGET_PACKAGES = "$RepoRoot\.nuget\packages"
}

$InstallArchitecture = $Architecture
if($Architecture.StartsWith("arm", [StringComparison]::OrdinalIgnoreCase))
{
    $InstallArchitecture = "x64"
}

$ArchitectureParam="/p:Architecture=$Architecture"
$ConfigurationParam="-configuration $Configuration"

Invoke-Expression "$RepoRoot\eng\common\build.ps1 -restore -build $ConfigurationParam $ArchitectureParam $ExtraParameters"
if($LASTEXITCODE -ne 0) { throw "Failed to build" }
