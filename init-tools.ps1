#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$Architecture="x64")

$RepoRoot = "$PSScriptRoot"

# Install a stage 0
Write-Host "Installing .NET Core CLI Stage 0 from branchinfo channel"
    
& "$RepoRoot\scripts\obtain\dotnet-install.ps1" -Channel $env:CHANNEL -Architecture $Architecture -Verbose
if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }