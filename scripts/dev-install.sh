#!/usr/bin/env bash

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
REPOROOT="$( cd -P "$DIR/.." && pwd )"

source "$DIR/_common.sh"

if [ -z "$RID" ]; then
    UNAME=$(uname)
    if [ "$UNAME" == "Darwin" ]; then
        OSNAME=osx
        RID=osx.10.10-x64
    elif [ "$UNAME" == "Linux" ]; then
        # Detect Distro?
        OSNAME=linux
        RID=ubuntu.14.04-x64
    else
        echo "Unknown OS: $UNAME" 1>&2
        exit 1
    fi
fi

OUTPUT_ROOT=$REPOROOT/artifacts/$RID
DNX_DIR=$OUTPUT_ROOT/dnx
HOST_DIR=$OUTPUT_ROOT/clrhost
STAGE1_DIR=$OUTPUT_ROOT/stage1
STAGE2_DIR=$OUTPUT_ROOT/stage2

DESTINATION=$(eval echo "~/.dotnet/sdks/dotnet-${OSNAME}-x64.0.0.1-dev")

info "Symlinking $STAGE2_DIR to $DESTINATION"
rm -f $DESTINATION
ln -s $STAGE2_DIR $DESTINATION
