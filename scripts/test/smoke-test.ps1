#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\_common.ps1"

# Restore and compile the test app
dotnet restore "$RepoRoot\test\TestApp" --runtime "osx.10.10-x64" --runtime "ubuntu.14.04-x64" --runtime "win7-x64"
dotnet compile "$RepoRoot\test\TestApp" --output "$RepoRoot\artifacts\$Rid\smoketest"

# Run the app and check the exit code
& "$RepoRoot\artifacts\$Rid\smoketest\TestApp.exe"
if ($LASTEXITCODE -ne 0) {
    throw "Test App failed to run"
}

# Check that a compiler error is reported
$oldErrorAction = $ErrorActionPreference
$ErrorActionPreference="SilentlyContinue"
dotnet compile "$RepoRoot\test\compile\failing\SimpleCompilerError" --framework "$Tfm" 2>$null >$null
if ($LASTEXITCODE -eq 0) {
    throw "Compiler error didn't cause non-zero exit code!"
}
$ErrorActionPreference = $oldErrorAction
