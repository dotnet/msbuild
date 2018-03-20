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

REPO_ROOT="$( cd -P "$( dirname "$SOURCE" )/../" && pwd )"

uname=$(uname)
if [ "$(uname)" = "Darwin" ]
then
  RID=osx-x64
else
  RID=linux-x64
fi

STAGE2_DIR=$REPO_ROOT/bin/2/$RID/dotnet
export PATH=$STAGE2_DIR:$PATH


export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_MULTILEVEL_LOOKUP=0

export NUGET_PACKAGES=$REPO_ROOT/.nuget/packages
export TEST_PACKAGES=$REPO_ROOT/bin/2/$RID/test/packages
export TEST_ARTIFACTS=$REPO_ROOT/bin/2/$RID/test/artifacts
export PreviousStageProps=$REPO_ROOT/bin/2/$RID/PreviousStage.props
