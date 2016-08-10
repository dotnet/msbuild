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
DOTNET_CLI_VERSION=$(cat "$REPOROOT/DotnetCLIVersion.txt")

CONFIGURATION="Debug"
PLATFORM="Any CPU"

# Set nuget package cache under the repo
export NUGET_PACKAGES="$REPOROOT/packages"

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
[ -z "$DOTNET_INSTALL_DIR" ] && export DOTNET_INSTALL_DIR=$REPOROOT/.dotnet_cli
[ -d "$DOTNET_INSTALL_DIR" ] || mkdir -p $DOTNET_INSTALL_DIR

# Install a stage 0
DOTNET_INSTALL_SCRIPT_URL="https://raw.githubusercontent.com/dotnet/cli/feature/msbuild/scripts/obtain/dotnet-install.sh"
curl -sSL "$DOTNET_INSTALL_SCRIPT_URL" | bash /dev/stdin  --version $DOTNET_CLI_VERSION --verbose

# Put stage 0 on the PATH (for this shell only)
PATH="$DOTNET_INSTALL_DIR:$PATH"

# Disable first run since we want to control all package sources
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

echo "${args[@]}"

dotnet build3 $REPOROOT/build/build.proj /m /nologo /p:Configuration=$CONFIGURATION /p:Platform="$PLATFORM" "${args[@]}"