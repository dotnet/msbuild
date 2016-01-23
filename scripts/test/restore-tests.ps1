#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. $PSScriptRoot\..\common\_common.ps1

info "Restoring Test Projects"

# Restore packages
& dotnet restore "$RepoRoot\test" -f "$TestPackageDir"

