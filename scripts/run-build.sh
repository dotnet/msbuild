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

# Load Branch Info
while read line; do
    if [[ $line != \#* ]]; then
        IFS='=' read -ra splat <<< "$line"
        export ${splat[0]}="${splat[1]}"
    fi
done < "$DIR/../branchinfo.txt"

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot
[ -z "$DOTNET_INSTALL_DIR" ] && export DOTNET_INSTALL_DIR=$DIR/../.dotnet_stage0/$(uname)
[ -d $DOTNET_INSTALL_DIR ] || mkdir -p $DOTNET_INSTALL_DIR

# Ensure the latest stage0 is installed
$DIR/obtain/install.sh --channel $RELEASE_SUFFIX

# Put stage 0 on the PATH (for this shell only)
PATH="$DOTNET_INSTALL_DIR/bin:$PATH"

# Increases the file descriptors limit for this bash. It prevents an issue we were hitting during restore
FILE_DESCRIPTOR_LIMIT=$( ulimit -n )
if [ $FILE_DESCRIPTOR_LIMIT -lt 1024 ]
then
    echo "Increasing file description limit to 1024"
    ulimit -n 1024
fi

# Restore the build scripts
echo "Restoring Build Script projects..."
(
    cd $DIR
    dotnet restore
)

# Build the builder
echo "Compiling Build Scripts..."
dotnet publish "$DIR/dotnet-cli-build" -o "$DIR/dotnet-cli-build/bin" --framework dnxcore50

# Run the builder
echo "Invoking Build Scripts..."
echo "Configuration: $CONFIGURATION"

if [ -f "$DIR/dotnet-cli-build/bin/dotnet-cli-build" ]; then
    DOTNET_HOME="$DOTNET_INSTALL_DIR/share/dotnet/cli" $DIR/dotnet-cli-build/bin/dotnet-cli-build "$@"
    exit $?
else
    # We're on an older CLI. This is temporary while Ubuntu and CentOS VSO builds are stalled.
    DOTNET_HOME="$DOTNET_INSTALL_DIR/share/dotnet/cli" $DIR/dotnet-cli-build/bin/Debug/dnxcore50/dotnet-cli-build "$@"
    exit $?
fi
