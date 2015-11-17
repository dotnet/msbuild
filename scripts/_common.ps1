#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

$Rid = "win7-x64"
$Tfm = "dnxcore50"

$RepoRoot = Convert-Path "$PSScriptRoot\.."

$OutputDir = "$RepoRoot\artifacts\$Rid"
$Stage1Dir = "$OutputDir\stage1"
$Stage2Dir = "$OutputDir\stage2"
$HostDir = "$OutputDir\corehost"
$PackageDir = "$RepoRoot\artifacts\packages\dnvm"

function header([string]$message)
{
    Write-Host -ForegroundColor Green "*** $message ***"
}
