#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

function loadTestProjectList()
{
    return Import-CSV "$RepoRoot\scripts\configuration\testProjects.csv" -Header "ProjectName"
}

function loadTestScriptList()
{
    return Import-CSV "$RepoRoot\scripts\configuration\testScripts.csv" -Header "ProjectName"
}

function loadTestPackageList()
{
    return Import-CSV "$RepoRoot\scripts\configuration\testPackageProjects.csv" -Header "ProjectName"
}

function loadBuildProjectList()
{
    return Import-CSV "$RepoRoot\scripts\configuration\buildProjects.csv" -Header "ProjectName"
}