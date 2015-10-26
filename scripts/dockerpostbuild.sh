#!/usr/bin/env bash

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

cd $DIR/..

[ -z "$DOTNET_BUILD_CONTAINER_TAG" ] && DOTNET_BUILD_CONTAINER_TAG="dotnetcli-build"
[ -z "$DOTNET_BUILD_CONTAINER_NAME" ] && DOTNET_BUILD_CONTAINER_NAME="dotnetcli-build-container"
[ -z "$DOCKER_HOST_SHARE_DIR" ] && DOCKER_HOST_SHARE_DIR=$(pwd)

# Build the docker container (will be fast if it is already built)
docker build -t $DOTNET_BUILD_CONTAINER_TAG scripts/docker/

# First thing make sure all of our build containers are stopped
docker stop $DOTNET_BUILD_CONTAINER_NAME
docker rm $DOTNET_BUILD_CONTAINER_NAME

# Remove the sticky bit on directories created by docker so we can delete them
docker run --rm \
    -v $DOCKER_HOST_SHARE_DIR:/opt/code \
    -e DOTNET_BUILD_VERSION=$DOTNET_BUILD_VERSION \
    $DOTNET_BUILD_CONTAINER_TAG chmod -R -t /opt/code

# And Actually make those directories accessible to be deleted
docker run --rm \
    -v $DOCKER_HOST_SHARE_DIR:/opt/code \
    -e DOTNET_BUILD_VERSION=$DOTNET_BUILD_VERSION \
    $DOTNET_BUILD_CONTAINER_TAG chmod -R a+rwx /opt/code
