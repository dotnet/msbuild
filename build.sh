#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Set OFFLINE environment variable to build offline

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/scripts/common/_common.sh"
source "$REPOROOT/scripts/build/process-args.sh"

# splitting build from package is required to work around dotnet/coreclr#2215
# once that is fixed, we should remove the NOPACKAGE flag and do the full build either in
# or out of docker.
if [ ! -z "$BUILD_IN_DOCKER" ]; then
    export BUILD_COMMAND=". /opt/code/scripts/build/process-args.sh $@ ; . /opt/code/scripts/build/build.sh"
    $REPOROOT/scripts/docker/dockerbuild.sh
else
    $REPOROOT/scripts/build/build.sh
fi

if [ ! -z "$NOPACKAGE" ]; then
    header "Skipping packaging"
else
    if [ ! -z "$PACKAGE_IN_DOCKER" ]; then
        export BUILD_COMMAND="/opt/code/scripts/package/package.sh"
        $REPOROOT/scripts/docker/dockerbuild.sh
    else
        $REPOROOT/scripts/package/package.sh
    fi
fi
