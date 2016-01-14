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

TestBinRoot="$REPOROOT/artifacts/tests"

TestProjects=( \
    E2E \
    StreamForwarderTests \
    Microsoft.DotNet.Tools.Publish.Tests \
    Microsoft.DotNet.Tools.Compiler.Tests \
    Microsoft.DotNet.Tools.Builder.Tests \
)

for project in ${TestProjects[@]}
do
    dotnet publish --framework "dnxcore50" --output "$TestBinRoot" --configuration "$CONFIGURATION" "$REPOROOT/test/$project"
done

# copy TestProjects folder which is used by the test cases
mkdir -p "$TestBinRoot/TestProjects"
cp -a $REPOROOT/test/TestProjects/* $TestBinRoot/TestProjects


pushd "$TestBinRoot"
set +e

failedTests=()
failCount=0

for project in ${TestProjects[@]}
do
    ./corerun "xunit.console.netcore.exe" "$project.dll" -xml "${project}-testResults.xml" -notrait category=failing
    exitCode=$?
    failCount+=$exitCode
    if [ $exitCode -ne 0 ]; then
        failedTests+=("${project}.dll")
    fi
done

"$REPOROOT/scripts/test/package-command-test.sh"
if [ $? -ne 0 ]; then
    failCount+=1
    failedTests+=("package-command-test.sh")
fi

for test in ${failedTests[@]}
do
    error "$test failed. Logs in '$TestBinRoot/${test}-testResults.xml'"
done

popd
set -e

exit $failCount
