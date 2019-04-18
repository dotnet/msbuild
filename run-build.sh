#!/bin/bash
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
REPOROOT="$DIR"

ARCHITECTURE="x64"

source "$REPOROOT/scripts/common/_prettyprint.sh"

LINUX_PORTABLE_INSTALL_ARGS=
CUSTOM_BUILD_ARGS=

# Set nuget package cache under the repo
[ -z $NUGET_PACKAGES ] && export NUGET_PACKAGES="$REPOROOT/.nuget/packages"

args=( )

while [[ $# > 0 ]]; do
    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        -c|--configuration)
            export CONFIGURATION=$2
            args+=("--configuration")
            args+=("$2")
            shift
            ;;
        -a|--architecture)
            ARCHITECTURE="$2"
            args+=("/p:Architecture=$ARCHITECTURE")
            shift
            ;;
        --runtime-id)
            args+=("/p:Rid=\"$2\"")
            shift
            ;;
        --linux-portable)
            args+=("/p:Rid=linux-x64 /p:OSName=\"linux\" /p:IslinuxPortable=\"true\"")
            ;;
        --help)
            echo "Usage: $0 [--configuration <CONFIGURATION>] [--architecture <ARCHITECTURE>] [--docker <IMAGENAME>] [--help]"
            echo ""
            echo "Options:"
            echo "  --configuration <CONFIGURATION>     Build the specified Configuration (Debug or Release, default: Debug)"
            echo "  --architecture <ARCHITECTURE>       Build the specified architecture (x64, arm or arm64 , default: x64)"
            echo "  --docker <IMAGENAME>                Build in Docker using the Dockerfile located in scripts/docker/IMAGENAME"
            echo "  --help                              Display this help message"
            exit 0
            ;;
        *)
            args+=("$1")
            ;;
    esac

    shift
done

. "$REPOROOT/eng/common/build.sh" --build --restore "${args[@]}"
