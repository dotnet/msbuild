#!/usr/bin/env bash
#
# $1 is passed to package to enable deb or pkg packaging
set -e

for i in "$@"
    do
        lowerI="$(echo $i | awk '{print tolower($0)}')"
        case $lowerI in
        release)
            export CONFIGURATION=Release
            ;;
        debug)
            export CONFIGURATION=Debug
            ;;
        *)
        esac
    done

[ -z "$CONFIGURATION" ] && CONFIGURATION=Debug

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/scripts/_common.sh"

# UTC Timestamp of the last commit is used as the build number. This is for easy synchronization of build number between Windows, OSX and Linux builds.
LAST_COMMIT_TIMESTAMP=$(git log -1 --format=%ct)

if [ "$(uname)" == "Darwin" ]; then
    export DOTNET_BUILD_VERSION=0.0.1-alpha-$(date -ur $LAST_COMMIT_TIMESTAMP "+%Y%m%d-%H%M%S")
else
    export DOTNET_BUILD_VERSION=0.0.1-alpha-$(date -ud @$LAST_COMMIT_TIMESTAMP "+%Y%m%d-%H%M%S")
fi

header "Building dotnet tools version $DOTNET_BUILD_VERSION - $CONFIGURATION"

if [ ! -z "$BUILD_IN_DOCKER" ]; then
    export BUILD_COMMAND="/opt/code/scripts/compile.sh"
    $DIR/scripts/docker/dockerbuild.sh
else
    $DIR/scripts/compile.sh
fi

if [ ! -z "$PACKAGE_IN_DOCKER" ]; then
    export BUILD_COMMAND="/opt/code/scripts/package/package.sh"
    $DIR/scripts/docker/dockerbuild.sh
else
    $DIR/scripts/package/package.sh
fi
