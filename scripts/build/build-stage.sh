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
REPOROOT="$( cd -P "$DIR/../.." && pwd )"

source "$DIR/../_common.sh"

[ ! -z "$TFM" ] || die "Missing required environment variable TFM"
[ ! -z "$RID" ] || die "Missing required environment variable RID"
[ ! -z "$CONFIGURATION" ] || die "Missing required environment variable CONFIGURATION"
[ ! -z "$OUTPUT_DIR" ] || die "Missing required environment variable OUTPUT_DIR"
[ ! -z "$HOST_DIR" ] || die "Missing required environment variable HOST_DIR"

PROJECTS=( \
    Microsoft.DotNet.Cli \
    Microsoft.DotNet.Tools.Compiler \
    Microsoft.DotNet.Tools.Compiler.Csc \
    Microsoft.DotNet.Tools.Publish \
    Microsoft.DotNet.Tools.Pack \
    Microsoft.DotNet.Tools.Repl \
    Microsoft.DotNet.Tools.Repl.Csi \
    Microsoft.DotNet.Tools.Resgen \
    Microsoft.DotNet.Tools.Run \
    Microsoft.DotNet.Tools.Init \
    Microsoft.DotNet.Tools.Compiler.Native \
)

BINARIES_FOR_COREHOST=( \
    csi \
    csc \
    vbc \
)

FILES_TO_CLEAN=( \
    README.md \
    Microsoft.DotNet.Runtime \
    Microsoft.DotNet.Runtime.dll \
    Microsoft.DotNet.Runtime.deps \
    Microsoft.DotNet.Runtime.pdb \
)

# Clean up output
[ -d "$OUTPUT_DIR" ] && rm -Rf "$OUTPUT_DIR"

RUNTIME_OUTPUT_DIR="$OUTPUT_DIR/runtime/coreclr"

for project in ${PROJECTS[@]}
do
    dotnet publish --framework "$TFM" --runtime "$RID" --output "$OUTPUT_DIR/bin" --configuration "$CONFIGURATION" "$REPOROOT/src/$project"
done

# Bring in the runtime
dotnet publish --framework "$TFM" --runtime "$RID" --output "$RUNTIME_OUTPUT_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Runtime"

# Clean up bogus additional files
for file in ${FILES_TO_CLEAN[@]}
do
    [ -e "$RUNTIME_OUTPUT_DIR/$file" ] && rm "$RUNTIME_OUTPUT_DIR/$file"
done

# Copy the runtime app-local for the tools
cp -R $RUNTIME_OUTPUT_DIR/* $OUTPUT_DIR/bin

# Deploy CLR host to the output
cp "$HOST_DIR/corehost" "$OUTPUT_DIR/bin"

# corehostify externally-provided binaries (csc, vbc, etc.)
for binary in ${BINARIES_FOR_COREHOST[@]}
do
    cp $OUTPUT_DIR/bin/corehost $OUTPUT_DIR/bin/$binary
    mv $OUTPUT_DIR/bin/${binary}.exe $OUTPUT_DIR/bin/${binary}.dll
done

# remove any deps files that got brought along (they aren't needed because we have an app-local runtime and dependencies)
rm $OUTPUT_DIR/bin/*.deps

cd $OUTPUT_DIR

# Fix up permissions. Sometimes they get dropped with the wrong info
find . -type f | xargs chmod 644
$DIR/fix-mode-flags.sh
