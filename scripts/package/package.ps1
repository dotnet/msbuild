#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\common\_common.ps1"
. "$RepoRoot\scripts\build\generate-version.ps1"

header "Generating zip package"
_ "$RepoRoot\scripts\package\package-zip.ps1"

header "Generating dotnet MSI"
_ "$RepoRoot\packaging\windows\generatemsi.ps1" @("$Stage2Dir")

header "Generating NuGet packages"
_ "$RepoRoot\packaging\nuget\package.ps1" @("$Stage2Dir\bin", "$env:VersionSuffix")
