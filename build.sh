#!/usr/bin/env bash
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
DOTNET_CLI_VERSION="$(cat "$REPOROOT/DotnetCLIVersion.txt")"

CONFIGURATION="Debug"
PLATFORM="Any CPU"

args=( "$@" )

while [[ $# > 0 ]]; do
    lowerI="$(echo $1 | awk '{print tolower($0)}')"
    case $lowerI in
        -c|--configuration)
            CONFIGURATION=$2
            args=( "${args[@]/$1}" )
            args=( "${args[@]/$2}" )
            shift
            ;;
        --platform)
            PLATFORM=$2
            args=( "${args[@]/$1}" )
            args=( "${args[@]/$2}" )
            shift
            ;;
        --help)
            echo "Usage: $0 [--configuration <CONFIGURATION>] [--platform <PLATFORM>] [--help]"
            echo ""
            echo "Options:"
            echo "  --configuration <CONFIGURATION>     Build the specified Configuration (Debug or Release, default: Debug)"
            echo "  --platform <PLATFORM>               Skip checks for pre-reqs in dotnet_install"
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

# Set nuget package cache under the repo
export NUGET_PACKAGES="$REPOROOT/packages"
export NUGET_HTTP_CACHE_PATH="$REPOROOT/packages"

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
[ -z "$DOTNET_INSTALL_DIR" ] && export DOTNET_INSTALL_DIR=$REPOROOT/.dotnet_cli
[ -d "$DOTNET_INSTALL_DIR" ] || mkdir -p $DOTNET_INSTALL_DIR

# NuGet depends on HOME and it may not be set. Until it's fixed, we just patch a value in
if [ -z "$HOME" ]; then
    export HOME="$REPOROOT/.home"

    [ ! -d "$HOME" ] || rm -Rf $HOME
    mkdir -p $HOME
fi

# Install a stage 0
DOTNET_INSTALL_SCRIPT_URL="https://dot.net/v1/dotnet-install.sh"
curl -sSL "$DOTNET_INSTALL_SCRIPT_URL" | bash /dev/stdin  --version $DOTNET_CLI_VERSION --verbose

# Install 1.0.4 shared framework
[ -d "$DOTNET_INSTALL_DIR/shared/Microsoft.NETCore.App/1.0.5" ] || curl -sSL "$DOTNET_INSTALL_SCRIPT_URL" | bash /dev/stdin --version "1.0.5" --shared-runtime

# Install 1.1.1 shared framework
[ -d "$DOTNET_INSTALL_DIR/shared/Microsoft.NETCore.App/1.1.2" ] || curl -sSL "$DOTNET_INSTALL_SCRIPT_URL" | bash /dev/stdin --version "1.1.2" --shared-runtime

# Put stage 0 on the PATH
export PATH="$DOTNET_INSTALL_DIR:$PATH"

# Disable first run since we want to control all package sources
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Don't resolve runtime, shared framework, or SDK from global locations
export DOTNET_MULTILEVEL_LOOKUP=0

logPath="$REPOROOT/bin/log"
[ -d "$logPath" ] || mkdir -p $logPath

# NET Core Build
msbuildSummaryLog="$logPath/sdk.log"
msbuildWarningLog="$logPath/sdk.wrn"
msbuildFailureLog="$logPath/sdk.err"
msbuildBinLog="$logPath/sdk.binlog"

dotnet msbuild $REPOROOT/build/build.proj /m:1 /nologo /p:Configuration=$CONFIGURATION /p:Platform="$PLATFORM" /warnaserror /flp1:Summary\;Verbosity=diagnostic\;Encoding=UTF-8\;LogFile=$msbuildSummaryLog /flp2:WarningsOnly\;Verbosity=diagnostic\;Encoding=UTF-8\;LogFile=$msbuildWarningLog /flp3:ErrorsOnly\;Verbosity=diagnostic\;Encoding=UTF-8\;LogFile=$msbuildFailureLog /bl:$msbuildBinLog "${args[@]}"
