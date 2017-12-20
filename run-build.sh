#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

set -e

machine_has() {
    hash "$1" > /dev/null 2>&1
    return $?
}

check_min_reqs() {
    if ! machine_has "curl"; then
        echo "run-build: Error: curl is required to download dotnet. Install curl to proceed." >&2
        return 1
    fi
    return 0
}

# args:
# remote_path - $1
# [out_path] - $2 - stdout if not provided
download() {
    eval $invocation
    
    local remote_path=$1
    local out_path=${2:-}

    local failed=false
    if [ -z "$out_path" ]; then
        curl --retry 10 -sSL --create-dirs $remote_path || failed=true
    else
        curl --retry 10 -sSL --create-dirs -o $out_path $remote_path || failed=true
    fi
    
    if [ "$failed" = true ]; then
        echo "run-build: Error: Download failed" >&2
        return 1
    fi
}

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
ALL_LINUX_INSTALLERS_TARGET=
GENERATE_INSTALLERS_TARGET=
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
        --architecture)
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
            CUSTOM_BUILD_ARGS="/p:Rid=\"linux-x64\" /p:OSName=\"linux\" /p:IslinuxPortable=\"true\""
            ;;
        --all-linux-installers)
            ALL_LINUX_INSTALLERS_TARGET="/t:BuildAndPublishAllLinuxDistrosNativeInstallers"
            ;;
        --generate-installers)
            GENERATE_INSTALLERS_TARGET="/t:GenerateInstallersAndCopyOutOfSandBox"
            ;;
        --stage0)
            STAGE0_SOURCE_DIR=$2
            shift
            ;;
        --help)
            echo "Usage: $0 [--configuration <CONFIGURATION>] [--skip-prereqs] [--nopackage] [--docker <IMAGENAME>] [--help]"
            echo ""
            echo "Options:"
            echo "  --configuration <CONFIGURATION>     Build the specified Configuration (Debug or Release, default: Debug)"
            echo "  --skip-prereqs                      Skip checks for pre-reqs in dotnet_install"
            echo "  --nopackage                         Skip packaging targets"
            echo "  --nobuild                           Skip building, showing the command that would be used to build"
            echo "  --docker <IMAGENAME>                Build in Docker using the Dockerfile located in scripts/docker/IMAGENAME"
            echo "  --linux-portable                    Builds the Linux portable .NET Tools instead of a distro-specific version."
            echo "  --all-linux-installers              Builds and publishes all the Linux distros' native installers; outer call"
            echo "                                          Note: used primarily for 'AllLinuxDistrosNativeInstallers' VSO build."
            echo "  --generate-installers               Builds and publishes all the Linux distros' native installers; inner call"
            echo "                                          Note: used primarily for 'AllLinuxDistrosNativeInstallers' VSO build."
            echo "  --stage0                            Set the stage0 source directory. The default is to download it from Azure."
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

# Create an install directory for the stage 0 CLI
[ -z "$DOTNET_INSTALL_DIR" ] && export DOTNET_INSTALL_DIR=$REPOROOT/.dotnet_stage0/$ARCHITECTURE
[ -d "$DOTNET_INSTALL_DIR" ] || mkdir -p $DOTNET_INSTALL_DIR

export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Enable verbose VS Test Console logging
export VSTEST_BUILD_TRACE=1
export VSTEST_TRACE_BUILD=1


# Don't resolve shared frameworks from user or global locations
export DOTNET_MULTILEVEL_LOOKUP=0

# Install a stage 0
if [ "$STAGE0_SOURCE_DIR" == "" ]; then
    (set -x ; "$REPOROOT/scripts/obtain/dotnet-install.sh" --version "2.2.0-preview1-007799" --install-dir "$DOTNET_INSTALL_DIR" --architecture "$ARCHITECTURE" $LINUX_PORTABLE_INSTALL_ARGS)
else
    echo "Copying bootstrap cli from $STAGE0_SOURCE_DIR"
    cp -r $STAGE0_SOURCE_DIR/* "$DOTNET_INSTALL_DIR"
fi

EXIT_CODE=$?
if [ $EXIT_CODE != 0 ]; then
    echo "run-build: Error: installing stage0 with exit code $EXIT_CODE." >&2
    exit $EXIT_CODE
fi

# Put stage 0 on the PATH (for this shell only)
PATH="$DOTNET_INSTALL_DIR:$PATH"

# Increases the file descriptors limit for this bash. It prevents an issue we were hitting during restore
FILE_DESCRIPTOR_LIMIT=$( ulimit -n )
if [ $FILE_DESCRIPTOR_LIMIT -lt 1024 ]
then
    echo "Increasing file description limit to 1024"
    ulimit -n 1024
fi

# Disable first run since we want to control all package sources
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

echo "${args[@]}"

if [ $BUILD -eq 1 ]; then
    dotnet msbuild build.proj /p:Architecture=$ARCHITECTURE $CUSTOM_BUILD_ARGS /p:GeneratePropsFile=true /t:WriteDynamicPropsToStaticPropsFiles $args
    dotnet msbuild build.proj /m /v:normal /fl /flp:v=diag /p:Architecture=$ARCHITECTURE $CUSTOM_BUILD_ARGS $ALL_LINUX_INSTALLERS_TARGET $GENERATE_INSTALLERS_TARGET $args
else
    echo "Not building due to --nobuild"
    echo "Command that would be run is: 'dotnet msbuild build.proj /m /p:Architecture=$ARCHITECTURE $CUSTOM_BUILD_ARGS $ALL_LINUX_INSTALLERS_TARGET $GENERATE_INSTALLERS_TARGET $args'"
fi
