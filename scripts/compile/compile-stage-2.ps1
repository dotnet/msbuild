#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. $PSScriptRoot\..\common\_common.ps1

header "Compiling stage2 dotnet using stage1 ..."
$StartPath = $env:PATH
$env:PATH = "$Stage1Dir\bin;$env:PATH"

# Compile
_ "$RepoRoot\scripts\compile\compile-stage.ps1" @("$Tfm","$Rid","$Configuration","$Stage2Dir","$RepoRoot","$HostDir")

# Crossgen Roslyn
header "Crossgening Roslyn compiler ..."
_cmd "$RepoRoot\scripts\crossgen\crossgen_roslyn.cmd ""$Stage2Dir"""

# Copy dnx into stage 2
cp -rec "$DnxRoot\" "$Stage2Dir\bin\dnx\"

# Copy in the dotnet-restore script
cp "$RepoRoot\scripts\dotnet-restore.cmd" "$Stage2Dir\bin\dotnet-restore.cmd"

# Copy in AppDeps
$env:PATH = "$Stage2Dir\bin;$StartPath"
header "Acquiring Native App Dependencies"
_cmd "$RepoRoot\scripts\build\build_appdeps.cmd ""$Stage2Dir""" 

$env:PATH=$StartPath