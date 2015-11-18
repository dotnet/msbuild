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
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
REPOROOT="$( cd -P "$DIR/.." && pwd )"

source "$DIR/_common.sh"

if ! type -p cmake >/dev/null; then
    error "cmake is required to build the native host 'corehost'"
    error "OS X w/Homebrew: 'brew install cmake'"
    error "Ubuntu: 'sudo apt-get install cmake'"
    exit 1
fi

[ -z "$CONFIGURATION" ] && export CONFIGURATION=Debug

REPOROOT="$( cd -P "$DIR/.." && pwd )"

# Ensure the latest stage0 is installed
$DIR/install.sh

# And put the stage0 on the PATH
export PATH=$REPOROOT/artifacts/$RID/stage0/bin:$PATH

# Intentionally clear the DOTNET_TOOLS path, we want to use the default installed version
unset DOTNET_TOOLS

DOTNET_PATH=$(which dotnet)
PREFIX="$(cd -P "$(dirname "$DOTNET_PATH")/.." && pwd)"
DNX_ROOT="$PREFIX/share/dotnet/cli/bin/dnx"
if [ ! -d "$DNX_ROOT" ]; then
    echo "warning: your stage0 is using the old layout" 1>&2
    DNX_ROOT="$PREFIX/share/dotnet/cli/dnx"
fi

if [ ! -d "$DNX_ROOT" ] || [ ! -e "$DNX_ROOT/dnx" ]; then
    echo "error: could not find DNX in $DNX_ROOT" 1>&2
    exit 1
fi

header "Restoring packages"
dotnet restore "$REPOROOT" --quiet --runtime "osx.10.10-x64" --runtime "ubuntu.14.04-x64" --runtime "win7-x64" --no-cache

header "Building corehost"

# Set up the environment to be used for building with clang.
if which "clang-3.5" > /dev/null 2>&1; then
    export CC="$(which clang-3.5)"
    export CXX="$(which clang++-3.5)"
elif which "clang-3.6" > /dev/null 2>&1; then
    export CC="$(which clang-3.6)"
    export CXX="$(which clang++-3.6)"
elif which clang > /dev/null 2>&1; then
    export CC="$(which clang)"
    export CXX="$(which clang++)"
else
    error "Unable to find Clang Compiler"
    error "Install clang-3.5 or clang3.6"
    exit 1
fi

pushd "$REPOROOT/src/corehost" 2>&1 >/dev/null
[ -d "cmake/$RID" ] || mkdir -p "cmake/$RID"
cd "cmake/$RID"
cmake ../.. -G "Unix Makefiles" -DCMAKE_BUILD_TYPE:STRING=$CONFIGURATION
make

# Publish to artifacts
[ -d "$HOST_DIR" ] || mkdir -p $HOST_DIR
cp "$REPOROOT/src/corehost/cmake/$RID/corehost" $HOST_DIR
popd 2>&1 >/dev/null

# Build Stage 1
header "Building stage1 dotnet using downloaded stage0 ..."
OUTPUT_DIR=$STAGE1_DIR $DIR/build/build-stage.sh

# Use stage1 tools
export DOTNET_TOOLS=$STAGE1_DIR

# Build Stage 2
header "Building stage2 dotnet using just-built stage1 ..."
OUTPUT_DIR=$STAGE2_DIR $DIR/build/build-stage.sh

echo "Crossgenning Roslyn compiler ..."
$REPOROOT/scripts/crossgen/crossgen_roslyn.sh "$STAGE2_DIR/bin"

# Make Stage 2 Folder Accessible
chmod -R a+r $REPOROOT

# Copy DNX in to stage2
cp -R $DNX_ROOT $STAGE2_DIR/bin/dnx

# Copy and CHMOD the dotnet-restore script
cp $DIR/dotnet-restore.sh $STAGE2_DIR/bin/dotnet-restore
chmod a+x $STAGE2_DIR/bin/dotnet-restore

# Copy in AppDeps
header "Acquiring Native App Dependencies"
DOTNET_HOME=$STAGE2_DIR DOTNET_TOOLS=$STAGE2_DIR $REPOROOT/scripts/build/build_appdeps.sh "$STAGE2_DIR/bin"

# Stamp the output with the commit metadata
COMMIT_ID=$(git rev-parse HEAD)
echo $COMMIT_ID > $STAGE2_DIR/.commit

# Smoke-test the output
header "Testing stage2 ..."
DOTNET_HOME=$STAGE2_DIR DOTNET_TOOLS=$STAGE2_DIR $DIR/test/smoke-test.sh
