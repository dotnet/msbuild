#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
 
# Debian Packaging Script
# Currently Intended to build on ubuntu14.04

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/../_common.sh"

if [ "$UNAME" != "Linux" ]; then
    error "Debian Package build only supported on Linux"
    exit 1
fi

REPO_ROOT=$(readlink -f $DIR/../..)
PACKAGING_ROOT=$REPO_ROOT/packaging/debian

OUTPUT_DIR="$REPO_ROOT/artifacts"
PACKAGE_LAYOUT_DIR="$OUTPUT_DIR/deb_intermediate"
PACKAGE_OUTPUT_DIR="$OUTPUT_DIR/packages/debian"
REPO_BINARIES_DIR="$REPO_ROOT/artifacts/ubuntu.14.04-x64/stage2"

execute(){
    create_empty_debian_layout
    copy_files_to_debian_layout
    create_debian_package
    test_debian_package
}

create_empty_debian_layout(){
    header "Creating empty debian package layout"

    rm -rf $PACKAGE_LAYOUT_DIR
    mkdir -p $PACKAGE_LAYOUT_DIR

    mkdir "$PACKAGE_LAYOUT_DIR/\$"
    mkdir "$PACKAGE_LAYOUT_DIR/package_root"
    mkdir "$PACKAGE_LAYOUT_DIR/samples"
    mkdir "$PACKAGE_LAYOUT_DIR/docs"
}

copy_files_to_debian_layout(){
    header "Copying files to debian layout"

    # Copy Built Binaries
    cp -a "$REPO_BINARIES_DIR/." "$PACKAGE_LAYOUT_DIR/package_root"

    # Copy config file
    cp "$PACKAGING_ROOT/debian_config.json" "$PACKAGE_LAYOUT_DIR"
}

create_debian_package(){
    header "Packing .deb"

    mkdir -p $PACKAGE_OUTPUT_DIR

    $PACKAGING_ROOT/package_tool/package_tool $PACKAGE_LAYOUT_DIR $PACKAGE_OUTPUT_DIR $DOTNET_BUILD_VERSION
}

test_debian_package(){
    header "Testing debian package"

    git clone https://github.com/sstephenson/bats.git /tmp/bats
    pushd /tmp/bats
    ./install.sh /usr/local
    popd

    bats $PACKAGE_OUTPUT_DIR/test_package.bats
}

execute

DEBIAN_FILE=$(find $PACKAGE_OUTPUT_DIR -iname "*.deb")
$DIR/../publish/publish.sh $DEBIAN_FILE 
