#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\common\_common.ps1"

if(!(Test-Path $PackageDir)) {
    mkdir $PackageDir | Out-Null
}

if(![string]::IsNullOrEmpty($env:DOTNET_CLI_VERSION)) {
    $PackageVersion = $env:DOTNET_CLI_VERSION
} else {
    $Timestamp = [DateTime]::Now.ToString("yyyyMMddHHmmss")
    $PackageVersion = "0.0.1-dev-t$Timestamp"
}

# Stamp the output with the commit metadata and version number
$Commit = git rev-parse HEAD

$VersionContent = @"
$Commit
$PackageVersion
"@

$VersionContent | Out-File -Encoding UTF8 "$Stage2Dir\.version"

$PackageName = Join-Path $PackageDir "dotnet-win-x64.$PackageVersion.zip"

if (Test-Path $PackageName)
{
    del $PackageName
}

Add-Type -Assembly System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($Stage2Dir, $PackageName, "Optimal", $false)

Write-Host "Packaged stage2 to $PackageName"

$PublishScript = Join-Path $PSScriptRoot "..\publish\publish.ps1"
& $PublishScript -file $PackageName

exit $LastExitCode
