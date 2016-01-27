#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. $PSScriptRoot\..\common\_common.ps1

# Restore packages
# NOTE(anurse): I had to remove --quiet, because NuGet3 is too quiet when that's provided :(
header "Restoring packages"
dotnet restore "$RepoRoot\src" --runtime "$Rid"
dotnet restore "$RepoRoot\test" --runtime "$Rid"
dotnet restore "$RepoRoot\tools" --runtime "$Rid"

$oldErrorAction=$ErrorActionPreference
$ErrorActionPreference="SilentlyContinue"
dotnet restore "$RepoRoot\testapp" --runtime "$Rid" 2>&1 | Out-Null
$ErrorActionPreference=$oldErrorAction

