#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Set OFFLINE environment variable to build offline

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/../common/_common.sh"
source "$REPOROOT/scripts/build/generate-version.sh"

header "Building dotnet tools version $DOTNET_CLI_VERSION - $CONFIGURATION"
header "Checking Pre-Reqs"

$REPOROOT/scripts/test/check-prereqs.sh

header "Restoring Tools and Packages"

if [ ! -z "$OFFLINE" ]; then
    info "Skipping Tools and Package Download: Offline build"
else
   $REPOROOT/scripts/obtain/install-tools.sh
    
   $REPOROOT/scripts/build/restore-packages.sh
fi

header "Compiling"
$REPOROOT/scripts/compile/compile.sh

header "Setting Stage2 as PATH, DOTNET_HOME, and DOTNET_TOOLS"
export DOTNET_HOME=$STAGE2_DIR && export DOTNET_TOOLS=$STAGE2DIR && export PATH=$STAGE2_DIR/bin:$PATH 

header "Running Tests"
$REPOROOT/scripts/test/runtests.sh

header "Validating Dependencies"
$REPOROOT/scripts/test/validate-dependencies.sh
