#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#
param(
    [Parameter(Mandatory=$true)][string]$RepoRoot,
    [Parameter(Mandatory=$true)][string]$OutputDir)

$intermediateDir = "$RepoRoot\artifacts\appdepssdk\packages"
$appdepBinDir = "$OutputDir\bin\appdepsdk"

If (Test-Path $intermediateDir){
    rmdir -Force -Rec  $intermediateDir
}
mkdir $intermediateDir
& dotnet restore --packages "$intermediateDir" "$RepoRoot\src\dotnet-compile-native\appdep\project.json"


If (Test-Path $appdepBinDir){
    rmdir -Force -Rec  $appdepBinDir
}
mkdir -Force "$appdepBinDir"

ls "$intermediateDir\toolchain*\*\*" | foreach { 
    copy -Rec $_ "$appdepBinDir"
}