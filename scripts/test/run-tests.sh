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

TestProjects=$(loadTestProjectList)
TestScripts=$(loadTestScriptList)

failedTests=()
failCount=0

# Copy TestProjects to $TEST_BIN_ROOT
mkdir -p "$TEST_BIN_ROOT/TestProjects"
cp -a $REPOROOT/test/TestProjects/* $TEST_BIN_ROOT/TestProjects

pushd "$TEST_BIN_ROOT"
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
do
    scriptName=${script}.sh

    "$REPOROOT/scripts/test/${scriptName}"
    exitCode=$?
    if [ $exitCode -ne 0 ]; then
        failedTests+=("$scriptName")
        failCount+=1
    fi
done

for test in ${failedTests[@]}
do
    error "$test failed. Logs in '$TEST_BIN_ROOT/${test}-testResults.xml'"
done

set -e

exit $failCount
