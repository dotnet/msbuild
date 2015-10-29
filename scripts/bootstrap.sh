#!/usr/bin/env bash

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/_common.sh"

[ -z "$CONFIGURATION" ] && CONFIGURATION=Debug

# TODO: Replace this with a dotnet generation
TFM=dnxcore50

REPOROOT="$( cd -P "$DIR/.." && pwd )"

START_PATH=$PATH

OUTPUT_ROOT=$REPOROOT/artifacts/$RID
DNX_DIR=$OUTPUT_ROOT/dnx
STAGE1_DIR=$OUTPUT_ROOT/stage1
STAGE2_DIR=$OUTPUT_ROOT/stage2

banner "Installing stage0"

source $DIR/dnvm2.sh
dnvm upgrade -a dotnet_stage0
DNX_ROOT="$(dirname $(which dotnet))/dnx"

banner "Restoring packages"

dotnet restore "$REPOROOT" --runtime osx.10.10-x64 --runtime ubuntu.14.04-x64 --runtime osx.10.11-x64 --quiet

# Clean up stage1
[ -d "$STAGE1_DIR" ] && rm -Rf "$STAGE1_DIR"

banner "Building stage1 using downloaded stage0"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE1_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Cli"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE1_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Compiler"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE1_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Compiler.Csc"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE1_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Publish"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE1_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Resgen"

# Add stage1 to the path and use it to build stage2
export PATH=$STAGE1_DIR:$START_PATH

# Make corerun explicitly executable
chmod a+x $STAGE1_DIR/corerun

# Clean up stage2
[ -d "$STAGE2_DIR" ] && rm -Rf "$STAGE2_DIR"

banner "Building stage2 dotnet using compiled stage1 ..."
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE2_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Cli"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE2_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Compiler"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE2_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Compiler.Csc"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE2_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Publish"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE2_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Resgen"

# Make Stage 2 Folder Accessible
chmod -R a+r $REPOROOT

# Copy DNX in to stage2
cp -R $DNX_ROOT $STAGE2_DIR/dnx

# Copy and CHMOD the dotnet-restore script
cp $DIR/dotnet-restore.sh $STAGE2_DIR/dotnet-restore
chmod a+x $STAGE2_DIR/dotnet-restore

# Smoke-test the output
banner "Testing stage2 ..."
export PATH=$STAGE2_DIR:$START_PATH

# rm "$REPOROOT/test/TestApp/project.lock.json"
# dotnet restore "$REPOROOT/test/TestApp" --runtime "$RID"
# dotnet publish "$REPOROOT/test/TestApp" --framework "$TFM" --runtime "$RID" --output "$REPOROOT/artifacts/$RID/smoketest"

OUTPUT=$($REPOROOT/artifacts/$RID/smoketest/TestApp)
[ "$OUTPUT" == "This is a test app" ] || (error "Smoke test failed!" && exit 1)

# Check that a compiler error is reported
set +e
dotnet compile "$REPOROOT/test/compile/failing/SimpleCompilerError" --framework "$TFM" 2>/dev/null >/dev/null
rc=$?
if [ $rc == 0 ]; then
    error "Compiler failure test failed! The compiler did not fail to compile!"
    exit 1
fi
set -e
