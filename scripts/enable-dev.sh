#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# This script is NOT executable. It MUST be sourced!

if [ ! -z "$BASH_SOURCE" ]; then
    SOURCE="${BASH_SOURCE}"
elif [ ! -z "$ZSH_VERSION" ]; then
    SOURCE="$0"
else
    echo "Unsupported shell, this requires bash or zsh" 1>&2
    exit 1
fi

while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
REPOROOT="$( cd -P "$DIR/.." && pwd )"

UNAME=$(uname)

if [ -z "$RID" ]; then
    if [ "$UNAME" == "Darwin" ]; then
        OSNAME=osx
        RID=osx.10.10-x64
    elif [ "$UNAME" == "Linux" ]; then
        # Detect Distro?
        OSNAME=linux
        RID=ubuntu.14.04-x64
    else
        error "unknown OS: $UNAME" 1>&2
        exit 1
    fi
fi

export DOTNET_TOOLS=$REPOROOT/artifacts/$RID/stage2
