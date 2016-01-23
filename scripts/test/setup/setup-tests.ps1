#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\..\common\_common.ps1"

header "Test Setup: Restoring Prerequisites"
_ "$RepoRoot\scripts\test\setup\restore-test-prerequisites.ps1"

header "Test Setup: Building Prerequisites"
_ "$RepoRoot\scripts\test\setup\build-test-prerequisites.ps1"