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

# Build Stage 2
header "Building stage2 dotnet using just-built stage1 ..."
OUTPUT_DIR=$STAGE2_DIR $REPOROOT/scripts/compile/compile-stage.sh

echo "Crossgenning Roslyn compiler ..."
$REPOROOT/scripts/crossgen/crossgen_roslyn.sh "$STAGE2_DIR/bin"

# Make Stage 2 Folder Accessible
chmod -R a+r $REPOROOT

# Copy DNX in to stage2
cp -R $DNX_ROOT $STAGE2_DIR/bin/dnx

# Copy and CHMOD the dotnet-restore script
cp $REPOROOT/scripts/dotnet-restore.sh $STAGE2_DIR/bin/dotnet-restore
chmod a+x $STAGE2_DIR/bin/dotnet-restore

# No compile native support in centos yet
# https://github.com/dotnet/cli/issues/453
if [ "$OSNAME" != "centos"  ]; then
    # Copy in AppDeps
    header "Acquiring Native App Dependencies"
    DOTNET_HOME=$STAGE2_DIR DOTNET_TOOLS=$STAGE2_DIR $REPOROOT/scripts/build/build_appdeps.sh "$STAGE2_DIR/bin"
fi

# Stamp the output with the commit metadata
COMMIT=$(git rev-parse HEAD)
echo $COMMIT > $STAGE2_DIR/.version
echo $DOTNET_BUILD_VERSION >> $STAGE2_DIR/.version

export DOTNET_HOME=$STAGE2_DIR 
export DOTNET_TOOLS=$STAGE2_DIR 