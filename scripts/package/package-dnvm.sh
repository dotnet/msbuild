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

if [ ! -d "$STAGE2_DIR" ]; then
    error "missing stage2 output in $STAGE2_DIR" 1>&2
    exit
fi

PACKAGE_DIR=$REPOROOT/artifacts/packages/dnvm
[ -d "$PACKAGE_DIR" ] || mkdir -p $PACKAGE_DIR

PACKAGE_SHORT_NAME=dotnet-${OSNAME}-x64.${DOTNET_CLI_VERSION}
PACKAGE_NAME=$PACKAGE_DIR/${PACKAGE_SHORT_NAME}.tar.gz

cd $STAGE2_DIR

header "Packaging $PACKAGE_SHORT_NAME"

# Tar up the stage2 artifacts
# We need both "*" and ".version" to ensure we pick up that file
tar -czf $PACKAGE_NAME * .version

info "Packaged stage2 from '$STAGE2_DIR' to '$PACKAGE_NAME'"

$REPOROOT/scripts/publish/publish.sh $PACKAGE_NAME
