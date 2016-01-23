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

source "$DIR/../common/_common.sh"

#compile tests with direct dependencies
for test in $(ls -l "$REPOROOT/test/PackagedCommands/Consumers" | grep ^d | awk '{print $9}' | grep "Direct") 
do
    pushd "$REPOROOT/test/PackagedCommands/Consumers/$test"
    dotnet compile
    popd
done

#run test
for test in $(ls -l "$REPOROOT/test/PackagedCommands/Consumers" | grep ^d | awk '{print $9}' | grep "AppWith")
do
    testName="test/PackagedCommands/Consumers/$test"

    pushd "$REPOROOT/$testName"

    output=$(dotnet hello)

    rm "project.json"

    if [ "$output" == "Hello" ] ;
    then
        echo "Test Passed: $testName"
    else
        error "Test Failed: $testName/dotnet hello"
        error "             printed $output"
        exit 1
    fi

    popd
done
