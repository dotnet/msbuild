#!/usr/bin/env bash
set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Some things depend on HOME and it may not be set. We should fix those things, but until then, we just patch a value in
if [ -z "$HOME" ]; then
    export HOME=$SCRIPT_DIR/../artifacts/home

    [ ! -d "$HOME" ] || rm -Rf $HOME
    mkdir -p $HOME
fi

# Set the build number using CI build number
BASE_VERSION=0.0.2-alpha1
if [ ! -z "$BUILD_NUMBER" ]; then
    export DOTNET_BUILD_VERSION="$BASE_VERSION-$(printf "%05d" $BUILD_NUMBER)"
    echo "Building version $DOTNET_BUILD_VERSION"
fi

if [[ "$(uname)" == "Linux" ]]; then
    # Set Docker Container name to be unique
    container_name=""

    #Jenkins
    [ ! -z "$BUILD_TAG" ] && container_name="$BUILD_TAG"
    #VSO
    [ ! -z "$BUILD_BUILDID" ] && container_name="$BUILD_BUILDID"

    export DOTNET_BUILD_CONTAINER_NAME="$container_name"

    $SCRIPT_DIR/dockerbuild.sh debian $@
else
    $SCRIPT_DIR/../build.sh $@
fi
