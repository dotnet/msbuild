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

ArgTestRoot="$REPOROOT/test/ArgumentForwardingTests"
ArgTestOutputRoot="$REPOROOT/artifacts/tests/arg-forwarding"

dotnet publish --framework "dnxcore50" --output "$ArgTestOutputRoot" --configuration "$CONFIGURATION" "$ArgTestRoot/Reflector"
dotnet publish --framework "dnxcore50" --output "$ArgTestOutputRoot" --configuration "$CONFIGURATION" "$ArgTestRoot/ArgumentForwardingTests"



pushd "$ArgTestOutputRoot"
    ./corerun "xunit.console.netcore.exe" "ArgumentForwardingTests.dll" -xml "ArgumentForwardingTests-testResults.xml" -notrait category=failing
popd
