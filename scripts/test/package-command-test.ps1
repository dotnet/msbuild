#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\common\_common.ps1"

$TestPackagesPath = "$RepoRoot\artifacts\tests\package-command-test\packages"

if((Test-Path $TestPackagesPath) -eq 0)
{
    mkdir $TestPackagesPath;
}

"v1", "v2" |
foreach {
    dotnet pack "$RepoRoot\test\PackagedCommands\Commands\dotnet-hello\$_\dotnet-hello"
    cp "$RepoRoot\test\PackagedCommands\Commands\dotnet-hello\$_\dotnet-hello\bin\Debug\*.nupkg" -Destination $TestPackagesPath
    if (!$?) {
        error "Command failed: dotnet pack"
        Exit 1
    }
}

# workaround for dotnet-restore from the root failing for these tests since their dependencies aren't built yet
dir "$RepoRoot\test\PackagedCommands\Consumers" | where {$_.PsIsContainer} |
foreach {
    pushd "$RepoRoot\test\PackagedCommands\Consumers\$_"
    copy project.json.template project.json
    popd
}

#restore test projects
pushd "$RepoRoot\test\PackagedCommands\Consumers"
dotnet restore -s "$TestPackagesPath"
if (!$?) {
    error "Command failed: dotnet restore"
    Exit 1
}
popd

#compile apps
dir "$RepoRoot\test\PackagedCommands\Consumers" | where {$_.PsIsContainer} | where {$_.Name.Contains("Direct")} |
foreach {
    pushd "$RepoRoot\test\PackagedCommands\Consumers\$_"
    dotnet compile
    popd
}

#run test
dir "$RepoRoot\test\PackagedCommands\Consumers" | where {$_.PsIsContainer} | where  {$_.Name.Contains("AppWith")} |
foreach {
    $testName = "test\PackagedCommands\Consumers\$_" 
    pushd "$RepoRoot\$testName"
    $outputArray = dotnet hello | Out-String
    $output = [string]::Join('\n', $outputArray).Trim("`r", "`n")
    
    del "project.json"
    if ($output -ne "hello") {
        error "Test Failed: $testName\dotnet hello"
        error "             printed $output"
        Exit 1
    }
    
    info "Test passed: $testName"
    popd
}

Exit 0
