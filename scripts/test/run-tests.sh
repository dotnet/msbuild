#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/../common/_common.sh"

local TestProjects=$(loadTestProjectList)
local TestScripts=$(loadTestScriptList)

local failedTests=()
local failCount=0

# Copy TestProjects to $TestBinRoot
mkdir -p "$TestBinRoot/TestProjects"
cp -a $REPOROOT/test/TestProjects/* $TestBinRoot/TestProjects

pushd "$TestBinRoot"
set +e

for project in $TestProjects
do
    ./corerun "xunit.console.netcore.exe" "$project.dll" -xml "${project}-testResults.xml" -notrait category=failing
    exitCode=$?
    failCount+=$exitCode
    if [ $exitCode -ne 0 ]; then
        failedTests+=("${project}.dll")
    fi
done

popd

for script in $TestScripts
    local scriptName=${script}.sh

    "$REPOROOT/scripts/test/${scriptName}"
    exitCode=$?
    if [ $exitCode -ne 0 ]; then
        failedTests+=("$scriptName")
        failCount+=1
    fi
done

for test in ${failedTests[@]}
do
    error "$test failed. Logs in '$TestBinRoot/${test}-testResults.xml'"
done

set -e

exit $failCount
