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
[ -z "$BUILD_COMMAND" ] && BUILD_COMMAND="/opt/code/build.sh"

echo $DOCKER_HOST_SHARE_DIR

# Build the docker container (will be fast if it is already built)
docker build -t $DOTNET_BUILD_CONTAINER_TAG scripts/docker/

# Run the build in the container
docker run --rm --sig-proxy=true \
	--name $DOTNET_BUILD_CONTAINER_NAME \
    -v $DOCKER_HOST_SHARE_DIR:/opt/code \
    -e DOTNET_BUILD_VERSION=$DOTNET_BUILD_VERSION \
    -e DOTNET_CI_SKIP_STAGE0_INSTALL=$DOTNET_CI_SKIP_STAGE0_INSTALL \
    $DOTNET_BUILD_CONTAINER_TAG $BUILD_COMMAND $1
