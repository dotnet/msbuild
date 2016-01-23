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

. "$DIR/../common/_common.sh"

# Run Validation for Project.json dependencies
dotnet publish "$REPOROOT/tools/MultiProjectValidator" -o "$STAGE2_DIR/../tools" -c "$CONFIGURATION"
#TODO for release builds this should fail
set +e
PJ_VALIDATE_PATH="$STAGE2_DIR/../tools/$CONFIGURATION/$TFM"
if [ ! -d "$PJ_VALIDATE_PATH" ]
then
	PJ_VALIDATE_PATH="$STAGE2_DIR/../tools"
fi

"$PJ_VALIDATE_PATH/pjvalidate" "$REPOROOT/src"
set -e
