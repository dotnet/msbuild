#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. $PSScriptRoot\..\common\_common.ps1

header "Compiling stage2 dotnet using stage1 ..."
$StartPath = $env:PATH
$env:PATH = "$Stage1Dir\bin;$env:PATH"

# Compile
_ "$RepoRoot\scripts\compile\compile-stage.ps1" @("$Tfm","$Rid","$Configuration","$Stage2Dir","$RepoRoot","$HostDir", "$Stage2CompilationDir")


# Build the projects that we are going to ship as nuget packages
. $REPOROOT\scripts\package\projectsToPack.ps1

$ProjectsToPack | ForEach-Object {
    dotnet build --build-base-path "$Stage2Dir\forPackaging" --configuration "$Configuration" "$RepoRoot\src\$_"
    if (!$?) {
        Write-Host Command failed: dotnet build --native-subdirectory --build-base-path "$Stage2Dir\forPackaging" --configuration "$Configuration" "$RepoRoot\src\$_"
        exit 1
    }
}

$env:PATH=$StartPath