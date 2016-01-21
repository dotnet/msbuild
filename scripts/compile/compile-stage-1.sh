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

source "$DIR/../common/_common.sh"

export StartPath=$PATH
export PATH=$DOTNET_INSTALL_DIR/bin:$PATH

# Build Stage 1
header "Building stage1 dotnet using downloaded stage0 ..."
COMPILATION_OUTPUT_DIR=$STAGE1_COMPILATION_DIR OUTPUT_DIR=$STAGE1_DIR $REPOROOT/scripts/compile/compile-stage.sh

export PATH=$StartPath