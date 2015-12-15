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
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/../_common.sh"


TestBinRoot="$REPOROOT/artifacts/tests"

TestProjects=( \
    E2E \
    Microsoft.DotNet.Tools.Publish.Tests \
)


for project in ${TestProjects[@]}
do
    dotnet publish --framework "dnxcore50" --runtime "$RID" --output "$TestBinRoot" --configuration "$CONFIGURATION" "$REPOROOT/test/$project"
done

# copy TestProjects folder which is used by the test cases
mkdir -p "$TestBinRoot/TestProjects"
cp -R $REPOROOT/test/TestProjects/* $TestBinRoot/TestProjects

# set -e will abort if the exit code of this is non-zero
pushd "$TestBinRoot"

for project in ${TestProjects[@]}
do
    ./corerun  "xunit.console.netcore.exe" "$project.dll" -xml "project.xml"
done

popd
