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

$DIR/scripts/obtain/dotnet-install.sh --channel feature-msbuild --verbose

__init_tools_log=$DIR/init-tools.log
__BUILD_TOOLS_CLI_VERSION=$(cat "$DIR/BuildToolsCliVersion.txt")
__BUILD_TOOLS_DIR=$DIR/build_tools
__BUILD_TOOLS_CLI_DIR=$__BUILD_TOOLS_DIR/dotnetcli/
__BUILD_TOOLS_SOURCE=https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json
__BUILD_TOOLS_PACKAGE_VERSION=$(cat $DIR/BuildToolsVersion.txt)
__BUILD_TOOLS_PATH=$NUGET_PACKAGES/Microsoft.DotNet.BuildTools/$__BUILD_TOOLS_PACKAGE_VERSION/lib
__BUILD_TOOLS_SEMAPHORE=$__BUILD_TOOLS_DIR/init-tools.completed
__DOTNET_CMD=$__BUILD_TOOLS_CLI_DIR/dotnet
__PROJECT_JSON_PATH=$__BUILD_TOOLS_DIR/$__BUILD_TOOLS_PACKAGE_VERSION
__PROJECT_JSON_FILE=$__PROJECT_JSON_PATH/project.json
__PROJECT_JSON_CONTENTS="{ \"dependencies\": { \"Microsoft.DotNet.BuildTools\": \"$__BUILD_TOOLS_PACKAGE_VERSION\" }, \"frameworks\": { \"netcoreapp1.0\": { } } }"

if [ ! -e "$__PROJECT_JSON_FILE" ]; then
  mkdir -p "$__PROJECT_JSON_PATH"
  echo "$__PROJECT_JSON_CONTENTS" > "$__PROJECT_JSON_FILE"

  if [ ! -d "$__BUILD_TOOLS_CLI_DIR" ]; then
    echo "Installing Build Tools CLI Version: $__BUILD_TOOLS_CLI_VERSION"
    "$DIR/scripts/obtain/dotnet-install.sh" --channel rel-1.0.0 --version "$__BUILD_TOOLS_CLI_VERSION" --install-dir "$__BUILD_TOOLS_CLI_DIR"
  fi

  if [ ! -d "$__BUILD_TOOLS_PATH" ]; then
    echo "Restoring build tools version $__BUILD_TOOLS_PACKAGE_VERSION..."
    "$__DOTNET_CMD" restore "$__PROJECT_JSON_FILE" --packages "$NUGET_PACKAGES" --source "$__BUILD_TOOLS_SOURCE"

    if [ ! -e "$__BUILD_TOOLS_PATH/init-tools.sh" ]; then echo "ERROR: Could not restore build tools correctly. See '$__init_tools_log' for more details."; fi
    find .  
  fi  

  echo "Initializing build tools..."
  "$__BUILD_TOOLS_PATH/init-tools.sh" "$DIR" "$__DOTNET_CMD" "$__BUILD_TOOLS_DIR" >> "$__init_tools_log" 2>&1
  echo "Init-Tools completed for BuildTools Version: $__BUILD_TOOLS_PACKAGE_VERSION" > "$__BUILD_TOOLS_SEMAPHORE"
  echo "Done initializing tools"
else
  echo "Tools are already initialized"
fi

