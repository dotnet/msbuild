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

for project in loadTestList()
do
    dotnet publish --framework "dnxcore50" --runtime "$Rid" --output "$TestBinRoot" --configuration "$CONFIGURATION" "$REPOROOT/test/$project"
done

# TODO: Remove this when publish paths change back
cp -a "$TestBinRoot/Debug/dnxcore50/." "$TestBinRoot"
