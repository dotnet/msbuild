#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/../common/_common.sh"
source "$DIR/../common/_clang.sh"

header "Building corehost"

pushd "$REPOROOT/src/corehost" 2>&1 >/dev/null
[ -d "cmake/$RID" ] || mkdir -p "cmake/$RID"
cd "cmake/$RID"
cmake ../.. -G "Unix Makefiles" -DCMAKE_BUILD_TYPE:STRING=$CONFIGURATION
make

# Publish to artifacts
[ -d "$HOST_DIR" ] || mkdir -p $HOST_DIR
if [[ "$OSNAME" == "osx" ]]; then
   COREHOST_LIBNAME=libhostpolicy.dylib
else
   COREHOST_LIBNAME=libhostpolicy.so
fi
cp "$REPOROOT/src/corehost/cmake/$RID/cli/corehost" $HOST_DIR
cp "$REPOROOT/src/corehost/cmake/$RID/cli/dll/${COREHOST_LIBNAME}" $HOST_DIR
popd 2>&1 >/dev/null
