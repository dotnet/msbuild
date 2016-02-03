#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#
param(
    [Parameter(Mandatory=$true)][string]$RepoRoot,
    [Parameter(Mandatory=$true)][string]$OutputDir)

$appdepBinDir = "$OutputDir\bin\appdepsdk"

If (Test-Path $appdepBinDir){
    rmdir -Force -Rec  $appdepBinDir
}

mkdir -Force "$appdepBinDir"

ls "$env:NUGET_PACKAGES\toolchain.win7-x64.Microsoft.DotNet.AppDep\1.0.4-prerelease-00001\*" | foreach { 
    copy -Rec $_ "$appdepBinDir"
}