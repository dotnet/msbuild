#!/usr/bin/env bash
#
# $1 is passed to package to enable deb or pkg packaging

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

# UTC Timestamp of the last commit is used as the build number. This is for easy synchronization of build number between Windows, OSX and Linux builds.
LAST_COMMIT_TIMESTAMP=$(git log -1 --format=%ct)

if [ "$(uname)" == "Darwin" ]; then
    export DOTNET_BUILD_VERSION=0.0.1-alpha-$(date -ur $LAST_COMMIT_TIMESTAMP "+%Y%m%d-%H%M%S")
else
    export DOTNET_BUILD_VERSION=0.0.1-alpha-$(date -ud @$LAST_COMMIT_TIMESTAMP "+%Y%m%d-%H%M%S")
fi

echo Building dotnet tools verison - $DOTNET_BUILD_VERSION

$DIR/scripts/bootstrap.sh
$DIR/scripts/package.sh $1
