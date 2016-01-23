#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. $PSScriptRoot\..\common\_common.ps1

# Restore packages
header "Restoring packages"
& "$DnxRoot\dnu" restore "$RepoRoot\src" --quiet --runtime "$Rid"
& "$DnxRoot\dnu" restore "$RepoRoot\test" --quiet --runtime "$Rid"
& "$DnxRoot\dnu" restore "$RepoRoot\tools" --quiet --runtime "$Rid"

$oldErrorAction=$ErrorActionPreference
$ErrorActionPreference="SilentlyContinue"
& "$DnxRoot\dnu" restore "$RepoRoot\testapp" --quiet --runtime "$Rid" "$NoCacheArg" 2>&1 | Out-Null
$ErrorActionPreference=$oldErrorAction

