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

export REPOROOT="$(cd -P "$DIR/../.." && pwd)"

source "$DIR/../common/_common.sh"

if [ -z "$DOTNET_CLI_VERSION" ]; then
    TIMESTAMP=$(date "+%Y%m%d%H%M%S")
    DOTNET_CLI_VERSION=0.0.1-dev-t$TIMESTAMP
fi

VERSION_BADGE="$REPOROOT/resources/images/version_badge.svg"
BADGE_DESTINATION="$REPOROOT/artifacts/version_badge.svg"

header "Generating tarball"
$DIR/package-dnvm.sh

header "Generating Native Installer"
$DIR/package-native.sh

header "Generating version badge"
sed "s/ver_number/$DOTNET_CLI_VERSION/g" $VERSION_BADGE > $BADGE_DESTINATION

header "Publishing version badge"
$DIR/../publish/publish.sh $BADGE_DESTINATION

