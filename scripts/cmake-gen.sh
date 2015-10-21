#!/usr/bin/env bash

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/_common.sh"

# Generate make files for the coreclr host
CMAKE_OUTPUT=$DIR/../src/corehost/cmake/$RID
if [ ! -d $CMAKE_OUTPUT ]; then
    mkdir -p $CMAKE_OUTPUT
fi
pushd $CMAKE_OUTPUT
cmake ../.. -G "Unix Makefiles"
popd
