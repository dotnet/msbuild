#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\common\_common.ps1"

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
