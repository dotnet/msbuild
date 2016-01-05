#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$Configuration="Debug",
    [switch]$Offline,
    [switch]$NoCache)

$ErrorActionPreference="Stop"

. "$PSScriptRoot\..\common\_common.ps1"

_ "$RepoRoot\scripts\build\generate-version.ps1"

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

header "Running Tests"
_ "$RepoRoot\scripts\test\runtests.ps1"

header "Validating Dependencies"
_ "$RepoRoot\scripts\test\validate-dependencies.ps1"

header "Generating zip package"
_ "$RepoRoot\scripts\package\package.ps1"

header "Generating dotnet MSI"
_ "$RepoRoot\packaging\windows\generatemsi.ps1" @("$Stage2Dir")

header "Generating NuGet packages"
_ "$RepoRoot\packaging\nuget\package.ps1" @("$Stage2Dir\bin", "$VersionSuffix")