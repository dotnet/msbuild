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

rm "$REPOROOT/test/E2E/project.lock.json"
dotnet restore --quiet "$REPOROOT/test/E2E" --runtime "$RID"
dotnet publish --framework dnxcore50 --runtime "$RID" --output "$REPOROOT/artifacts/$RID/e2etest" "$REPOROOT/test/E2E" 

# set -e will abort if the exit code of this is non-zero
pushd "$REPOROOT/artifacts/$RID/e2etest"
mv ./E2E ./corehost
./corehost xunit.console.netcore.exe E2E.dll
popd
