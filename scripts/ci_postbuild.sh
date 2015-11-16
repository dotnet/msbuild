#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

if [[ "$(uname)" == "Linux" ]]; then
    # Set Docker Container name to be unique
    container_name=""

    #Jenkins
    [ ! -z "$BUILD_TAG" ] && container_name="$BUILD_TAG"
    #VSO
    [ ! -z "$BUILD_BUILDID" ] && container_name="$BUILD_BUILDID"

    export DOTNET_BUILD_CONTAINER_NAME="$container_name"

    $SCRIPT_DIR/docker/dockerpostbuild.sh $@
fi

ret_code=$?
exit $ret_code
