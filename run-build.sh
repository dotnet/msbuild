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
source "$REPOROOT/scripts/common/_prettyprint.sh"

BUILD=1

LINUX_PORTABLE_INSTALL_ARGS=
CUSTOM_BUILD_ARGS=

# Set nuget package cache under the repo
[ -z $NUGET_PACKAGES ] && export NUGET_PACKAGES="$REPOROOT/.nuget/packages"

args=( "$@" )

while [[ $# > 0 ]]; do
    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        -c|--configuration)
            export CONFIGURATION=$2
            args=( "${args[@]/$1}" )
            args=( "${args[@]/$2}" )
            shift
            ;;
        --nopackage)
            export DOTNET_BUILD_SKIP_PACKAGING=1
            args=( "${args[@]/$1}" )
            ;;
        --skip-prereqs)
            # Allow CI to disable prereqs check since the CI has the pre-reqs but not ldconfig it seems
            export DOTNET_INSTALL_SKIP_PREREQS=1
            args=( "${args[@]/$1}" )
            ;;
        --nobuild)
            BUILD=0
            ;;
        --architecture)
            ARCHITECTURE=$2
            args=( "${args[@]/$1}" )
            args=( "${args[@]/$2}" )
            shift
            ;;
        --runtime-id)
            CUSTOM_BUILD_ARGS="/p:Rid=\"$2\""
            args=( "${args[@]/$1}" )
            args=( "${args[@]/$2}" )
            shift
            ;;
        # This is here just to eat away this parameter because CI still passes this in.
        --targets)            
            args=( "${args[@]/$1}" )
            args=( "${args[@]/$2}" )
            shift
            ;;
        --linux-portable)
            LINUX_PORTABLE_INSTALL_ARGS="--runtime-id linux-x64"
            CUSTOM_BUILD_ARGS="/p:Rid=\"linux-x64\" /p:OSName=\"linux\""
            args=( "${args[@]/$1}" )
            ;;
        --help)
            echo "Usage: $0 [--configuration <CONFIGURATION>] [--targets <TARGETS...>] [--skip-prereqs] [--nopackage] [--docker <IMAGENAME>] [--help]"
            echo ""
            echo "Options:"
            echo "  --configuration <CONFIGURATION>     Build the specified Configuration (Debug or Release, default: Debug)"
            echo "  --skip-prereqs                      Skip checks for pre-reqs in dotnet_install"
            echo "  --nopackage                         Skip packaging targets"
            echo "  --nobuild                           Skip building, showing the command that would be used to build"
            echo "  --docker <IMAGENAME>                Build in Docker using the Dockerfile located in scripts/docker/IMAGENAME"
            echo "  --linux-portable                    Builds the Linux portable .NET Tools instead of a distro-specific version."
            echo "  --help                              Display this help message"
            exit 0
            ;;
        *)
            break
            ;;
    esac

    shift
done

# $args array may have empty elements in it.
# The easiest way to remove them is to cast to string and back to array.
# This will actually break quoted arguments, arguments like 
# -test "hello world" will be broken into three arguments instead of two, as it should.
temp="${args[@]}"
args=($temp)

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
[ -z "$DOTNET_INSTALL_DIR_PJ" ] && export DOTNET_INSTALL_DIR_PJ=$REPOROOT/.dotnet_stage0PJ/$ARCHITECTURE
[ -d "$DOTNET_INSTALL_DIR_PJ" ] || mkdir -p $DOTNET_INSTALL_DIR_PJ

# Also create an install directory for a post-PJnistic CLI 
[ -z "$DOTNET_INSTALL_DIR" ] && export DOTNET_INSTALL_DIR=$REPOROOT/.dotnet_stage0/$ARCHITECTURE
[ -d "$DOTNET_INSTALL_DIR" ] || mkdir -p $DOTNET_INSTALL_DIR

# During xplat bootstrapping, disable HTTP parallelism to avoid fatal restore timeouts.
export __INIT_TOOLS_RESTORE_ARGS="$__INIT_TOOLS_RESTORE_ARGS --disable-parallel"

# Enable verbose VS Test Console logging
export VSTEST_BUILD_TRACE=1
export VSTEST_TRACE_BUILD=1

DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
toolsLocalPath="$REPOROOT/build_tools"
if [ ! -z $BOOTSTRAP_INSTALL_DIR]; then
  toolsLocalPath = $BOOTSTRAP_INSTALL_DIR
fi
bootStrapperPath="$toolsLocalPath/bootstrap.sh"
dotnetInstallPath="$toolsLocalPath/dotnet-install.sh"
if [ ! -f $bootStrapperPath ]; then
    if [ ! -d $toolsLocalPath ]; then
        mkdir $toolsLocalPath
    fi
    download "https://raw.githubusercontent.com/dotnet/buildtools/master/bootstrap/bootstrap.sh" "$bootStrapperPath"
    chmod u+x $bootStrapperPath
fi

$bootStrapperPath --dotNetInstallBranch release/2.0.0 --repositoryRoot "$REPOROOT" --toolsLocalPath "$toolsLocalPath" --cliInstallPath $DOTNET_INSTALL_DIR_PJ --architecture $ARCHITECTURE > bootstrap.log
EXIT_CODE=$?
if [ $EXIT_CODE != 0 ]; then
    echo "run-build: Error: Boot-strapping failed with exit code $EXIT_CODE, see bootstrap.log for more information." >&2
    exit $EXIT_CODE
fi

# now execute the script
echo "installing CLI: $dotnetInstallPath --channel \"release/2.0.0\" --install-dir $DOTNET_INSTALL_DIR --architecture \"$ARCHITECTURE\" $LINUX_PORTABLE_INSTALL_ARGS"
$dotnetInstallPath --channel "release/2.0.0" --install-dir $DOTNET_INSTALL_DIR --architecture "$ARCHITECTURE" $LINUX_PORTABLE_INSTALL_ARGS
EXIT_CODE=$?
if [ $EXIT_CODE != 0 ]; then
    echo "run-build: Error: Boot-strapping post-PJ stage0 with exit code $EXIT_CODE." >&2
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
    dotnet msbuild build.proj /p:Architecture=$ARCHITECTURE $CUSTOM_BUILD_ARGS /p:GeneratePropsFile=true /t:WriteDynamicPropsToStaticPropsFiles
    dotnet msbuild build.proj /m /v:diag /fl /flp:v=diag /p:Architecture=$ARCHITECTURE $CUSTOM_BUILD_ARGS "${args[@]}"
else
    echo "Not building due to --nobuild"
    echo "Command that would be run is: 'dotnet msbuild build.proj /m /p:Architecture=$ARCHITECTURE $CUSTOM_BUILD_ARGS ${args[@]}'"
fi
