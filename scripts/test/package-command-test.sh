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

dotnet pack --output "$REPOROOT/artifacts/packages" "$REPOROOT/test/PackagedCommands/Commands/dotnet-hello/v1/dotnet-hello"
dotnet pack --output "$REPOROOT/artifacts/packages" "$REPOROOT/test/PackagedCommands/Commands/dotnet-hello/v2/dotnet-hello"

# enable restore for test projects
for test in `ls -l "$REPOROOT/test/PackagedCommands/Consumers" | grep ^d | awk '{print $9}' |
do
    pushd "$REPOROOT/test/PackagedCommands/Consumers/$test"
    cp "project.json.template" "project.json"
    popd
done

# restore test projects
pushd "$REPOROOT/test/PackagedCommands/Consumers"
dotnet restore -s "$REPOROOT/artifacts/packages" --no-cache --ignore-failed-sources --parallel
popd

#compile tests with direct dependencies
for test in `ls -l "$REPOROOT/test/PackagedCommands/Consumers" | grep ^d | awk '{print $9}' | grep "Direct"`
do
    pushd "$REPOROOT/test/PackagedCommands/Consumers/$test"
    dotnet compile
    popd
done

#run test
for test in `ls -l "$REPOROOT/test/PackagedCommands/Consumers" | grep ^d | awk '{print $9}' | grep "AppWith"`
do
    testName="test/PackagedCommands/Consumers/$test" 
    
    pushd "$REPOROOT/$testName"
    
    output=$(dotnet hello) 
    
    rm "project.json"
    
    if [ $testOutput != "hello" ] 
    then
        error "Test Failed: $testName/dotnet hello"
        error "             printed $testOutput"
        exit 1
    else
        echo "Test Passed: $testName"
    fi
    
    popd
done

exit 0