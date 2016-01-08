#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$Configuration="Debug",
    [switch]$Offline,
    [switch]$NoCache,
    [switch]$NoPackage)

$ErrorActionPreference="Stop"

. "$PSScriptRoot\..\common\_common.ps1"

. "$RepoRoot\scripts\build\generate-version.ps1"

header "Building dotnet tools version $($env:DOTNET_BUILD_VERSION) - $Configuration"
header "Checking Pre-Reqs"

_ "$RepoRoot\scripts\test\check-prereqs.ps1"

header "Restoring Tools and Packages"

if ($Offline){
    info "Skipping Tools and Packages dowlnoad: Offline build"
}
else {
    _ "$RepoRoot\scripts\obtain\install-tools.ps1"

    _ "$RepoRoot\scripts\build\restore-packages.ps1" @("$NoCache")
}

header "Compiling"
_ "$RepoRoot\scripts\compile\compile.ps1" @("$Configuration")

# Put stage2 on the PATH now that we have a build
$env:PATH = "$Stage2Dir\bin;$env:PATH"
$env:DOTNET_HOME = "$Stage2Dir"

header "Running Tests"
_ "$RepoRoot\scripts\test\runtests.ps1"

header "Validating Dependencies"
_ "$RepoRoot\scripts\test\validate-dependencies.ps1"

if ($NoPackage){
    info "Skipping Packaging"
}
else {
    _ "$RepoRoot\scripts\package\package.ps1"
}
