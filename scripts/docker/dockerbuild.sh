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

cd $REPOROOT

[ -z "$DOTNET_BUILD_CONTAINER_TAG" ] && DOTNET_BUILD_CONTAINER_TAG="dotnetcli-build"
[ -z "$DOTNET_BUILD_CONTAINER_NAME" ] && DOTNET_BUILD_CONTAINER_NAME="dotnetcli-build-container"
[ -z "$DOCKER_HOST_SHARE_DIR" ] && DOCKER_HOST_SHARE_DIR=$(pwd)
[ -z "$DOCKER_OS" ] && DOCKER_OS=$OSNAME
[ -z "$BUILD_COMMAND" ] && BUILD_COMMAND="/opt/code/scripts/build/build.sh"

# Build the docker container (will be fast if it is already built)
header "Building Docker Container"
docker build --build-arg USER_ID=$(id -u) -t $DOTNET_BUILD_CONTAINER_TAG scripts/docker/$DOCKER_OS

# Run the build in the container
header "Launching build in Docker Container"
info "Using code from: $DOCKER_HOST_SHARE_DIR"
docker run -t --rm --sig-proxy=true \
    --name $DOTNET_BUILD_CONTAINER_NAME \
    -v $DOCKER_HOST_SHARE_DIR:/opt/code \
    -e DOTNET_CLI_VERSION \
    -e SASTOKEN \
    -e STORAGE_ACCOUNT \
    -e STORAGE_CONTAINER \
    -e CHANNEL \
    -e CONNECTION_STRING \
    -e REPO_ID \
    -e REPO_USER \
    -e REPO_PASS \
    -e REPO_SERVER \
    $DOTNET_BUILD_CONTAINER_TAG \
    bash -c "${BUILD_COMMAND}"
