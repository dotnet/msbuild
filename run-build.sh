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
STAGE0_SOURCE_DIR=

source "$REPOROOT/scripts/common/_prettyprint.sh"

BUILD=1

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
            shift
            ;;
        --nopackage)
            export DOTNET_BUILD_SKIP_PACKAGING=1
            ;;
        --skip-prereqs)
            # Allow CI to disable prereqs check since the CI has the pre-reqs but not ldconfig it seems
            export DOTNET_INSTALL_SKIP_PREREQS=1
            ;;
        --nobuild)
            BUILD=0
            ;;
        -a|--architecture)
            ARCHITECTURE=$2
            shift
            ;;
        --runtime-id)
            CUSTOM_BUILD_ARGS="/p:Rid=\"$2\""
            shift
            ;;
        # This is here just to eat away this parameter because CI still passes this in.
        --targets)            
            shift
            ;;
        --linux-portable)
            LINUX_PORTABLE_INSTALL_ARGS="--runtime-id linux-x64"
            CUSTOM_BUILD_ARGS="/p:OSName=\"linux\" /p:IslinuxPortable=\"true\""
            ;;
        --stage0)
            STAGE0_SOURCE_DIR=$2
            shift
            ;;
        --help)
            echo "Usage: $0 [--configuration <CONFIGURATION>] [--architecture <ARCHITECTURE>] [--skip-prereqs] [--nopackage] [--nobuild ] [--docker <IMAGENAME>] [--stage0 <DIRECTORY>] [--help]"
            echo ""
            echo "Options:"
            echo "  --configuration <CONFIGURATION>     Build the specified Configuration (Debug or Release, default: Debug)"
            echo "  --architecture <ARCHITECTURE>       Build the specified architecture (x64, arm or arm64 , default: x64)"
            echo "  --skip-prereqs                      Skip checks for pre-reqs in dotnet_install"
            echo "  --nopackage                         Skip packaging targets"
            echo "  --nobuild                           Skip building, showing the command that would be used to build"
            echo "  --docker <IMAGENAME>                Build in Docker using the Dockerfile located in scripts/docker/IMAGENAME"
            echo "  --stage0 <DIRECTORY>                Set the stage0 source directory. The default is to download it from Azure."
            echo "  --help                              Display this help message"
            exit 0
            ;;
        *)
            args=$@
            break
            ;;
    esac

    shift
done

# The first 'pass' call to "dotnet msbuild build.proj" has a hard-coded "WriteDynamicPropsToStaticPropsFiles" target
#    therefore, this call should not have other targets defined. Remove all targets passed in as 'extra parameters'.
argsnotargets=( )
for arg in ${args[@]} 
do  
  arglower="$(echo $arg | awk '{print tolower($0)}')"
  if [[ $arglower != '/t:'* ]] && [[ $arglower != '/target:'* ]]; then
    argsnotargets+=($arg)
  fi
done

if [ $BUILD -eq 1 ]; then
    . "$REPOROOT/eng/common/build.sh" --build --restore "$@"
else
    echo "Not building due to --nobuild"
fi
