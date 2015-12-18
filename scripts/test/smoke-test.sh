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
REPOROOT="$( cd -P "$DIR/../.." && pwd )"

source "$DIR/../_common.sh"

rm "$REPOROOT/test/TestApp/project.lock.json"
dotnet restore "$REPOROOT/test/TestApp" --runtime "$RID"
dotnet compile "$REPOROOT/test/TestApp" --output "$REPOROOT/artifacts/$RID/smoketest"

# set -e will abort if the exit code of this is non-zero
$REPOROOT/artifacts/$RID/smoketest/TestApp

# Check that a compiler error is reported
set +e
dotnet compile "$REPOROOT/test/compile/failing/SimpleCompilerError" --framework "$TFM" 2>/dev/null >/dev/null
rc=$?
if [ $rc == 0 ]; then
    error "Compiler failure test failed! The compiler did not fail to compile!"
    exit 1
fi
set -e
