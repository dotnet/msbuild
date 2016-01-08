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

header "Restoring packages"

#Temporarily restore for ALL THE RIDS! This solves a bootstrapping problem in this fix
$DNX_ROOT/dnu restore "$REPOROOT/src" --quiet --runtime win7-x64 --runtime ubuntu.14.04-x64 --runtime osx.10.10-x64 --runtime osx.10.11-x64 --runtime centos.7.1-x64 "$NOCACHE" --parallel
$DNX_ROOT/dnu restore "$REPOROOT/test" --quiet --runtime win7-x64 --runtime ubuntu.14.04-x64 --runtime osx.10.10-x64 --runtime osx.10.11-x64 --runtime centos.7.1-x64 "$NOCACHE" --parallel
$DNX_ROOT/dnu restore "$REPOROOT/tools" --quiet --runtime win7-x64 --runtime ubuntu.14.04-x64 --runtime osx.10.10-x64 --runtime osx.10.11-x64 --runtime centos.7.1-x64 "$NOCACHE" --parallel
set +e
$DNX_ROOT/dnu restore "$REPOROOT/testapp" --quiet --runtime win7-x64 --runtime ubuntu.14.04-x64 --runtime osx.10.10-x64 --runtime osx.10.11-x64 --runtime centos.7.1-x64 "$NOCACHE" --parallel >/dev/null 2>&1
set -e
