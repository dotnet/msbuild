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
cp -rec -Force "$RepoRoot\test\TestProjects\*" "$TestBinRoot\TestProjects"

pushd "$TestBinRoot"
# Run each test project
$TestProjects | foreach {
    & ".\corerun" "xunit.console.netcore.exe" "$($_.ProjectName).dll" -xml "$($_.ProjectName)-testResults.xml" -notrait category=failing
    $exitCode = $LastExitCode
    if ($exitCode -ne 0) {
        $failingTests += "$($_.ProjectName)"
    }

    $failCount += $exitCode
}

popd

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
