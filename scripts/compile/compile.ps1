#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param([string]$Configuration = "Debug")

$ErrorActionPreference="Stop"

. $PSScriptRoot\..\common\_common.ps1

# Capture PATH for later
$StartPath = $env:PATH
$StartDotNetHome = $env:DOTNET_HOME

try {
    _ "$RepoRoot\scripts\compile\compile-corehost.ps1"

    _ "$RepoRoot\scripts\compile\compile-stage-1.ps1"
    
    # Issue https://github.com/dotnet/cli/issues/1294
    _ "$RepoRoot\scripts\build\restore-packages.ps1"
    
    _ "$RepoRoot\scripts\compile\compile-stage-2.ps1"
} finally {
    $env:PATH = $StartPath
    $env:DOTNET_HOME = $StartDotNetHome
}
