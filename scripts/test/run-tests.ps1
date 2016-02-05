#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\common\_common.ps1"

$TestBinRoot = "$RepoRoot\artifacts\tests"

$TestProjects = loadTestProjectList
$TestScripts = loadTestScriptList

$failCount = 0
$failingTests = @()

## Temporary Workaround for Native Compilation
## Need x64 Native Tools Dev Prompt Env Vars
## Tracked Here: https://github.com/dotnet/cli/issues/301
pushd "$env:VS140COMNTOOLS\..\..\VC"
cmd /c "vcvarsall.bat x64&set" |
foreach {
  if ($_ -match "=") {
    $v = $_.split("=", 2); set-item -force -literalpath "ENV:\$($v[0])" -value "$($v[1])"
  }
}
popd

# copy TestProjects to $TestBinRoot
mkdir -Force "$TestBinRoot\TestProjects"
cp -rec -Force "$RepoRoot\TestAssets\TestProjects\*" "$TestBinRoot\TestProjects"

# Run each test project
$TestProjects | foreach {
    # This is a workaroudn for issue #1184, where dotnet test needs to be executed from the folder containing the project.json.
    pushd "$RepoRoot\test\$($_.ProjectName)"
    dotnet test -xml "$TestBinRoot\$($_.ProjectName)-testResults.xml" -notrait category=failing
    popd

    $exitCode = $LastExitCode
    if ($exitCode -ne 0) {
        $failingTests += "$($_.ProjectName)"
    }

    $failCount += $exitCode
}

$TestScripts | foreach {
    $scriptName = "$($_.ProjectName).ps1"

    & "$RepoRoot\scripts\test\$scriptName"
    $exitCode = $LastExitCode
    if ($exitCode -ne 0) {
        $failingTests += "$scriptName"
        $failCount += 1
    }
}

if ($failCount -ne 0) {
    Write-Host -ForegroundColor Red "The following tests failed."
    $failingTests | foreach {
        Write-Host -ForegroundColor Red "$_.dll failed. Logs in '$TestBinRoot\$_-testResults.xml'"
    }
} else {
    Write-Host -ForegroundColor Green "All the tests passed!"
}

Exit $failCount
