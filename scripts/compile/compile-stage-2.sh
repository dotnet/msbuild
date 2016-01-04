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

# Use stage1 tools
export StartPath=$PATH
export DOTNET_TOOLS=$STAGE1_DIR
export PATH=$STAGE1_DIR/bin:$PATH

# Compile Stage 2
header "Compiling stage2 dotnet using just-built stage1 ..."
OUTPUT_DIR=$STAGE2_DIR $REPOROOT/scripts/compile/compile-stage.sh

export DOTNET_HOME=$STAGE2_DIR 
export DOTNET_TOOLS=$STAGE2_DIR 