#!/usr/bin/env bash

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

cd $DIR/..

# Add an option to override the Docker Host
HOST_CODE_DIR=$(pwd)
if [[ "$1" != "" ]]; then
    HOST_CODE_DIR=$1
fi

# Add an option to override the Script to Run
BUILD_SCRIPT=/opt/code/build.sh
if [[ "$2" != "" ]]; then
    BUILD_SCRIPT=$2
fi

[ -z "$DOTNET_BUILD_CONTAINER_TAG" ] && DOTNET_BUILD_CONTAINER_TAG="dotnetcli-build"
[ -z "$DOTNET_BUILD_CONTAINER_NAME" ] && DOTNET_BUILD_CONTAINER_NAME="dotnetcli-build-container"

# Build the docker container (will be fast if it is already built)
docker build -t $DOTNET_BUILD_CONTAINER_TAG scripts/docker/

# Run the build in the container
docker rm -f $DOTNET_BUILD_CONTAINER_NAME
docker run \
    -v $HOST_CODE_DIR:/opt/code \
    --name $DOTNET_BUILD_CONTAINER_NAME \
    -e DOTNET_BUILD_VERSION=$DOTNET_BUILD_VERSION \
    $DOTNET_BUILD_CONTAINER_TAG $BUILD_SCRIPT
