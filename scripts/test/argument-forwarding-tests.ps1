#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\common\_common.ps1"

$TestPackagesPath = "$RepoRoot\tests\packages"

$ArgTestRoot = "$RepoRoot\test\ArgumentForwardingTests"
$ArgTestOutputRoot = "$RepoRoot\artifacts\tests\arg-forwarding"

dotnet publish --framework "dnxcore50" --runtime "$Rid" --output "$ArgTestOutputRoot" --configuration "$Configuration" "$ArgTestRoot\Reflector"
if (!$?) {
    Write-Host Command failed: dotnet publish --framework "dnxcore50" --runtime "$Rid" --output "$ArgTestOutputRoot" --configuration "$Configuration" "$ArgTestRoot\Reflector"
    Exit 1
}

dotnet publish --framework "dnxcore50" --runtime "$Rid" --output "$ArgTestOutputRoot" --configuration "$Configuration" "$ArgTestRoot\ArgumentForwardingTests"
if (!$?) {
    Write-Host Command failed: dotnet publish --framework "dnxcore50" --runtime "$Rid" --output "$ArgTestOutputRoot" --configuration "$Configuration" "$ArgTestRoot\ArgumentForwardingTests"
    Exit 1
}

cp "$ArgTestRoot\Reflector\reflector_cmd.cmd" "$ArgTestOutputRoot"

pushd "$ArgTestOutputRoot"

& ".\corerun" "xunit.console.netcore.exe" "ArgumentForwardingTests.dll" -xml "$_-testResults.xml" -notrait category=failing
$exitCode = $LastExitCode

popd

# No need to output here, we'll get test results
if ($exitCode -ne 0) {
    Exit 1
}