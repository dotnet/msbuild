#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# $1 is passed to package to enable deb or pkg packaging
set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/scripts/_common.sh"

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

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
export DOTNET_INSTALL_DIR=$DIR/.dotnet_stage0/$RID
[ -d $DOTNET_INSTALL_DIR ] || mkdir -p $DOTNET_INSTALL_DIR
export PATH=$DOTNET_INSTALL_DIR/bin:$PATH

# UTC Timestamp of the last commit is used as the build number. This is for easy synchronization of build number between Windows, OSX and Linux builds.
LAST_COMMIT_TIMESTAMP=$(git log -1 --format=%ct)
major=1
minor=0
# no. of days since epoch
build=$(($LAST_COMMIT_TIMESTAMP/3600/24))
revision=$LAST_COMMIT_TIMESTAMP

export DOTNET_BUILD_VERSION=$major.$minor.$build.$revision

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
