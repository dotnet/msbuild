# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$toolsDir,
    [string]$versionSuffix = ""
)

# unify trailing backslash
$toolsDir = $toolsDir.TrimEnd('\')
$versionArg = ""
if ($versionSuffix -ne "") {
    $versionArg = "--version-suffix"
}

$RepoRoot = Convert-Path "$PSScriptRoot\..\.."

. "$RepoRoot\scripts\common\_common.ps1"
. "$RepoRoot\scripts\package\projectsToPack.ps1"

$IntermediatePackagesDir = "$RepoRoot\artifacts\packages\intermediate"
$PackagesDir = "$RepoRoot\artifacts\packages"

New-Item -ItemType Directory -Force -Path $IntermediatePackagesDir

foreach ($ProjectName in $ProjectsToPack) {
    $ProjectFile = "$RepoRoot\src\$ProjectName\project.json"

    & $toolsDir\dotnet pack "$ProjectFile" --no-build --build-base-path "$Stage2CompilationDir\forPackaging" --output "$IntermediatePackagesDir" --configuration "$env:CONFIGURATION" $versionArg $versionSuffix
    if (!$?) {
        Write-Host "$toolsDir\dotnet pack failed for: $ProjectFile"
        Exit 1
    }
}

Get-ChildItem $IntermediatePackagesDir -Filter *.nupkg | ? {$_.Name -NotLike "*.symbols.nupkg"} | Copy-Item -Destination $PackagesDir
