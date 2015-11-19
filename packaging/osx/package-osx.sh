#!/bin/bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

REPOROOT="$( cd -P "$DIR/../../" && pwd )"

if [ -z "$DOTNET_BUILD_VERSION" ]; then
    echo "Provide a version number (DOTNET_BUILD_VERSION) $DOTNET_BUILD_VERSION" && exit 1
fi

if [ "$(uname)" == "Darwin" ]; then
    OSNAME=osx
    RID=osx.10.10-x64
else
    echo "Package (OSX) only runs on Darwin"
    exit 0
fi

STAGE2_DIR=$REPOROOT/artifacts/$RID/stage2

if [ ! -d "$STAGE2_DIR" ]; then
    echo "Missing stage2 output in $STAGE2_DIR" 1>&2
    exit 1
fi

PACKAGE_DIR=$REPOROOT/artifacts/packages/pkg
[ -d "$PACKAGE_DIR" ] || mkdir -p $PACKAGE_DIR

PACKAGE_NAME=$PACKAGE_DIR/dotnet-cli-x64.${DOTNET_BUILD_VERSION}.pkg
#chmod -R 755 $STAGE2_DIR
pkgbuild --root $STAGE2_DIR \
         --version $DOTNET_BUILD_VERSION \
         --scripts $DIR/scripts \
         --ownership preserve \
         --identifier com.microsoft.dotnet.cli.pkg.dotnet-osx-x64 \
         --install-location /usr/local/share/dotnet/cli \
         $DIR/dotnet-osx-x64.$DOTNET_BUILD_VERSION.pkg

cat $DIR/Distribution-Template | sed "/{VERSION}/s//$DOTNET_BUILD_VERSION/g" > $DIR/Dist

productbuild --version $DOTNET_BUILD_VERSION --identifier com.microsoft.dotnet.cli --package-path $DIR --resources $DIR/resources --distribution $DIR/Dist $PACKAGE_NAME

#Clean temp files
rm $DIR/dotnet-osx-x64.$DOTNET_BUILD_VERSION.pkg
rm $DIR/Dist

$REPOROOT/scripts/publish/publish.sh $PACKAGE_NAME
