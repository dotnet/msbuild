#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

loadTestProjectList()
{
    echo $(cat "$REPOROOT/scripts/configuration/testProjects.csv")
}

loadTestScriptList()
{
    echo $(cat "$REPOROOT/scripts/configuration/testScripts.csv")
}

loadTestPackageList()
{
    echo $(cat "$REPOROOT/scripts/configuration/testPackageProjects.csv")
}

loadBuildProjectList()
{
    echo $(cat "$REPOROOT/scripts/configuration/buildProjects.csv")
}