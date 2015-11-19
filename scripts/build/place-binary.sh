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
SCRIPT_DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
REPOROOT="$( cd -P "$SCRIPT_DIR/../.." && pwd )"

source "$SCRIPT_DIR/../_common.sh"

echo "Copy From: $1"
echo "       To: $STAGE2_DIR/bin/"

src=${1//\\//}
dst=$STAGE2_DIR/bin/

if [ ! -d "$dst" ]; then
  mkdir -p $dst
fi

# Copy the files, if they exist
if ls $src 1> /dev/null 2>&1; then
    cp $src $dst
    rc=$?; if [[ $rc != 0 ]]; then exit $rc; fi
fi
