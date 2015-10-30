#!/usr/bin/env bash

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

OUTPUT_ROOT=$REPOROOT/artifacts/$RID
DNX_DIR=$OUTPUT_ROOT/dnx
STAGE1_DIR=$OUTPUT_ROOT/stage1
STAGE2_DIR=$OUTPUT_ROOT/stage2
HOST_DIR=$OUTPUT_ROOT/corehost

if ! type -p cmake >/dev/null; then
    error "cmake is required to build the native host 'corehost'"
    error "OS X w/Homebrew: 'brew install cmake'"
    error "Ubuntu: 'sudo apt-get install cmake'"
    exit 1
fi

[ -z "$CONFIGURATION" ] && CONFIGURATION=Debug

# TODO: Replace this with a dotnet generation
TFM=dnxcore50

REPOROOT="$( cd -P "$DIR/.." && pwd )"

START_PATH=$PATH

banner "Installing stage0"
[ -d "~/.dotnet" ] || mkdir -p "~/.dotnet"
source $DIR/dnvm2.sh
dnvm upgrade -a dotnet_stage0
DNX_ROOT="$(dirname $(which dotnet))/dnx"

banner "Restoring packages"

dotnet restore "$REPOROOT" --runtime osx.10.10-x64 --runtime ubuntu.14.04-x64 --runtime osx.10.11-x64 --quiet

# Clean up stage1
[ -d "$STAGE1_DIR" ] && rm -Rf "$STAGE1_DIR"

banner "Building corehost"
pushd "$REPOROOT/src/corehost" 2>&1 >/dev/null
[ -d "cmake/$RID" ] || mkdir -p "cmake/$RID"
cd "cmake/$RID"
cmake ../.. -G "Unix Makefiles" -DCMAKE_BUILD_TYPE:STRING=$CONFIGURATION
make

# Publish to artifacts
[ -d "$HOST_DIR" ] || mkdir -p $HOST_DIR
cp "$REPOROOT/src/corehost/cmake/$RID/corehost" $HOST_DIR
popd 2>&1 >/dev/null

banner "Building stage1 using downloaded stage0"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE1_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Cli"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE1_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Compiler"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE1_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Compiler.Csc"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE1_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Publish"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE1_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Resgen"

# Deploy CLR host to the output
cp "$HOST_DIR/corehost" "$STAGE1_DIR"

# Add stage1 to the path and use it to build stage2
export PATH=$STAGE1_DIR:$START_PATH

# Clean up stage2
[ -d "$STAGE2_DIR" ] && rm -Rf "$STAGE2_DIR"

banner "Building stage2 dotnet using compiled stage1 ..."
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE2_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Cli"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE2_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Compiler"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE2_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Compiler.Csc"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE2_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Publish"
dotnet publish --framework "$TFM" --runtime $RID --output "$STAGE2_DIR" --configuration "$CONFIGURATION" "$REPOROOT/src/Microsoft.DotNet.Tools.Resgen"

# Deploy CLR host to the output
cp "$HOST_DIR/corehost" "$STAGE2_DIR"

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

rm "$REPOROOT/test/TestApp/project.lock.json"
dotnet restore "$REPOROOT/test/TestApp"
dotnet compile "$REPOROOT/test/TestApp" --output "$REPOROOT/artifacts/$RID/smoketest"

export CLRHOST_CLR_PATH=$STAGE2_DIR

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
