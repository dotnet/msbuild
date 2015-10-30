#!/usr/bin/env bash
set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
SCRIPT_DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$SCRIPT_DIR/_common.sh"

# Some things depend on HOME and it may not be set. We should fix those things, but until then, we just patch a value in
if [ -z "$HOME" ]; then
    export HOME=$SCRIPT_DIR/../artifacts/home

    [ ! -d "$HOME" ] || rm -Rf $HOME
    mkdir -p $HOME
fi

# UTC Timestamp of the last commit is used as the build number. This is for easy synchronization of build number between Windows, OSX and Linux builds.
LAST_COMMIT_TIMESTAMP=$(git log -1 --format=%ct)
export DOTNET_BUILD_VERSION=0.0.1-alpha-$(date -ud @$LAST_COMMIT_TIMESTAMP "+%Y%m%d-%H%M%S")

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
