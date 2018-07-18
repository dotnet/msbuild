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

REPO_ROOT="$DIR/../.."
PROJECT_PATH="$DIR/update-dependencies.csproj"

# Some things depend on HOME and it may not be set. We should fix those things, but until then, we just patch a value in
if [ -z "${HOME:-}" ]; then
    export HOME=$REPO_ROOT/artifacts/home

    [ ! -d "$HOME" ] || rm -Rf "$HOME"
    mkdir -p "$HOME"
fi

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot)
if [ -z "${DOTNET_INSTALL_DIR:-}" ]; then
   export DOTNET_INSTALL_DIR=$REPO_ROOT/.dotnet_stage0/x64
fi

export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Install a stage 0
echo "Installing .NET Core CLI Stage 0"
$REPO_ROOT/scripts/obtain/dotnet-install.sh -Version 2.1.302 -Architecture x64

if [ $? -ne 0 ]; then
    echo "Failed to install stage 0"
    exit 1
fi

# Put the stage 0 on the path
export PATH=$DOTNET_INSTALL_DIR:$PATH

echo "Invoking App $PROJECT_PATH..."
dotnet run -p "$PROJECT_PATH" $@

if [ $? -ne 0 ]; then
    echo "Build failed"
    exit 1
fi
