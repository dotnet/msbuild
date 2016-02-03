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

# Always recalculate the RID because the package always uses a specific RID, regardless of OS X version or Linux distro.
if [ "$OSNAME" == "osx" ]; then
    RID=osx.10.10-x64
elif [ "$OSNAME" == "ubuntu" ]; then
    RID=ubuntu.14.04-x64
elif [ "$OSNAME" == "centos" ]; then
    RID=centos.7.1-x64
else
    echo "Unknown OS: $OSNAME" 1>&2
    exit 1
fi

# Get Absolute Output Dir
pushd $1
OUTPUT_DIR="$(pwd)"
popd

## App Deps ##
APPDEP_SDK=$NUGET_PACKAGES/toolchain.$RID.Microsoft.DotNet.AppDep/1.0.5-prerelease-00001/
mkdir -p $OUTPUT_DIR/appdepsdk
cp -a $APPDEP_SDK/. $OUTPUT_DIR/appdepsdk
