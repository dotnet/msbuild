#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Set OFFLINE environment variable to build offline

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

# Some things depend on HOME and it may not be set. We should fix those things, but until then, we just patch a value in
if [ -z "$HOME" ]; then
    export HOME=$DIR/artifacts/home

    [ ! -d "$HOME" ] || rm -Rf $HOME
    mkdir -p $HOME
fi

while [[ $# > 0 ]]; do
    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        -c|--configuration)
            export CONFIGURATION=$2
            shift
            ;;
        --nopackage)
            export DOTNET_BUILD_SKIP_PACKAGING=1
            ;;
        --docker)
            export BUILD_IN_DOCKER=1
            export DOCKER_IMAGENAME=$2
            shift
            ;;
        --skip-prereqs)
            # Allow CI to disable prereqs check since the CI has the pre-reqs but not ldconfig it seems
            export DOTNET_INSTALL_SKIP_PREREQS=1
            ;;
        --help)
            echo "Usage: $0 [--configuration <CONFIGURATION>] [--skip-prereqs] [--nopackage] [--docker <IMAGENAME>] [--help] <TARGETS...>"
            echo ""
            echo "Options:"
            echo "  --configuration <CONFIGURATION>     Build the specified Configuration (Debug or Release, default: Debug)"
            echo "  --nopackage                         Skip packaging targets"
            echo "  --skip-prereqs                      Skip checks for pre-reqs in dotnet_install"
            echo "  --docker <IMAGENAME>                Build in Docker using the Dockerfile located in scripts/docker/IMAGENAME"
            echo "  --help                              Display this help message"
            echo "  <TARGETS...>                        The build targets to run (Init, Compile, Publish, etc.; Default is a full build and publish)"
            exit 0
            ;;
        *)
            break
            ;;
    esac

    shift
done

# Check if we need to build in docker
if [ ! -z "$BUILD_IN_DOCKER" ]; then
    $DIR/scripts/dockerbuild.sh "$@"
else
    $DIR/scripts/run-build.sh "$@"
fi
