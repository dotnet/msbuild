#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

set -e

export CI_BUILD=1
export NO_COLOR=1

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
SCRIPT_DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

while [[ $# > 0 ]]; do
    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        "release" | "--release")
            export CONFIGURATION=Release
            ;;
        "debug" | "--debug")
            export CONFIGURATION=Debug
            ;;
        "offline" | "--offline")
            export OFFLINE=true
            ;;
        "nopackage" | "--nopackage")
            export NOPACKAGE=true
            ;;
        "--buildindocker-ubuntu")
            export BUILD_IN_DOCKER=1
            export DOCKER_IMAGENAME=ubuntu
            ;;
        "--buildindocker-centos")
            export BUILD_IN_DOCKER=1
            export DOCKER_IMAGENAME=centos
            ;;
        *)
            break
            ;;
    esac

    shift
done

# Tell install scripts to skip pre-req check since the CI has the pre-reqs but not ldconfig it seems
# Also, install to a directory under the repo root since we don't have permission to work elsewhere
export DOTNET_INSTALL_SKIP_PREREQS=1

# Some things depend on HOME and it may not be set. We should fix those things, but until then, we just patch a value in
if [ -z "$HOME" ]; then
    export HOME=$SCRIPT_DIR/../artifacts/home

    [ ! -d "$HOME" ] || rm -Rf $HOME
    mkdir -p $HOME
fi

# Set Docker Container name to be unique
container_name=""

#Jenkins
[ ! -z "$BUILD_TAG" ] && container_name="$BUILD_TAG"

#VSO
[ ! -z "$BUILD_BUILDID" ] && container_name="$BUILD_BUILDID"

export DOTNET_BUILD_CONTAINER_NAME="$container_name"

## CentOS-based CI machines don't have docker, ditto OSX. So only build in docker if we're on Ubuntu
#if [ "$(cat /etc/*-release | grep -cim1 ubuntu)" -eq 1 ]; then
    #export BUILD_IN_DOCKER=1
#fi

VERBOSE=1 $SCRIPT_DIR/../build.sh "$@"
