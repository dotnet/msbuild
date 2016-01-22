#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. $PSScriptRoot\..\common\_common.ps1

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
if (!(Test-Path $env:DOTNET_INSTALL_DIR))
{
    mkdir $env:DOTNET_INSTALL_DIR | Out-Null
}

# Install a stage 0
header "Installing dotnet stage 0"
_ "$RepoRoot\scripts\obtain\install.ps1" @("$env:Channel")
