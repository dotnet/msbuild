#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\common\_common.ps1"

header "Setting up Tests"
_ "$RepoRoot\scripts\test\setup\setup-tests.ps1"

header "Restoring test projects"
_ "$RepoRoot\scripts\test\restore-tests.ps1"

header "Building test projects"
_ "$RepoRoot\scripts\test\build-tests.ps1"

header "Running Tests"
_ "$RepoRoot\scripts\test\run-tests.ps1"