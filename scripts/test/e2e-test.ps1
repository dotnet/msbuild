#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\_common.ps1"

# Restore and compile the test app
dotnet restore "$RepoRoot\test\E2E" --runtime "osx.10.10-x64" --runtime "ubuntu.14.04-x64" --runtime "win7-x64"
if (!$?) {
    Write-Host "Command failed: dotnet restore"
    Exit 1
}

dotnet publish --framework dnxcore50 --runtime "$Rid" --output "$RepoRoot\artifacts\$Rid\e2etest" "$RepoRoot\test\E2E"
if (!$?) {
    Write-Host "Command failed: dotnet publish"
    Exit 1
}

## Temporary Workaround for Native Compilation
## Need x64 Native Tools Dev Prompt Env Vars
## Tracked Here: https://github.com/dotnet/cli/issues/301
pushd "$env:VS140COMNTOOLS\..\..\VC"
cmd /c "vcvarsall.bat x64&set" |
foreach {
  if ($_ -match "=") {
    $v = $_.split("="); set-item -force -path "ENV:\$($v[0])"  -value "$($v[1])"
  }
}
popd

# Run the app and check the exit code
pushd "$RepoRoot\artifacts\$Rid\e2etest"
& "CoreRun.exe" "xunit.console.netcore.exe" "E2E.dll"
if (!$?) {
    Write-Host "E2E Test Failure"
    popd
    Exit 1
}
else {
    popd
}
