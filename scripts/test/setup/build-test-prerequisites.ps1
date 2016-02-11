#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\..\common\_common.ps1"

function buildTestPackages
{
    mkdir -Force $TestPackageDir

    loadTestPackageList | foreach {
        dotnet pack "$RepoRoot\TestAssets\TestPackages\$($_.ProjectName)" --output "$TestPackageDir"
    
        if (!$?) {
            error "Command failed: dotnet pack"
            Exit 1
        }
    }
}


function buildTestProjects
{
    $testProjectsRoot = "$RepoRoot\TestAssets\TestProjects"
    $exclusionList = @("$testProjectsRoot\CompileFail\project.json")
    $testProjectsList = (Get-ChildItem "$testProjectsRoot" -rec -filter "project.json").FullName

    $testProjectsList | foreach {
        if ($exclusionList -notcontains $_) {

            Write-Host "$_"
            dotnet build "$_"

            if (!$?) {
                error "Command failed: dotnet build"
                Exit 1
            }
        }
    }
}

buildTestPackages

buildTestProjects
