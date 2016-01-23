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
SCRIPT_DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$SCRIPT_DIR/common/_common.sh"

# Tell install scripts to skip pre-req check since the CI has the pre-reqs but not ldconfig it seems
# Also, install to a directory under the repo root since we don't have permission to work elsewhere
export DOTNET_INSTALL_SKIP_PREREQS=1

# Some things depend on HOME and it may not be set. We should fix those things, but until then, we just patch a value in
if [ -z "$HOME" ]; then
    export HOME=$SCRIPT_DIR/../artifacts/home

    [ ! -d "$HOME" ] || rm -Rf $HOME
    mkdir -p $HOME
fi

# Set Docker Container name to be unique
container_name=""

#Jenkins
[ ! -z "$BUILD_TAG" ] && container_name="$BUILD_TAG"
#VSO
[ ! -z "$BUILD_BUILDID" ] && container_name="$BUILD_BUILDID"

export DOTNET_BUILD_CONTAINER_NAME="$container_name"


if [[ "$OSNAME" == "ubuntu" ]]; then
    export PACKAGE_IN_DOCKER="true"
    unset BUILD_IN_DOCKER

    $SCRIPT_DIR/../build.sh $@
else
    $SCRIPT_DIR/../build.sh $@
fi
