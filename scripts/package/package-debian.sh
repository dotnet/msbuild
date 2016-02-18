#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
 
# Debian Packaging Script
# Currently Intended to build on ubuntu14.04

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/../common/_common.sh"

if [ "$OSNAME" != "ubuntu" ]; then
    error "Debian Package build only supported on Ubuntu"
    exit 1
fi

PACKAGING_ROOT="$REPOROOT/packaging/debian"
PACKAGING_TOOL_DIR="$REPOROOT/tools/DebianPackageTool"

OUTPUT_DIR="$REPOROOT/artifacts"
PACKAGE_LAYOUT_DIR="$OUTPUT_DIR/deb_intermediate"
PACKAGE_OUTPUT_DIR="$OUTPUT_DIR/packages/debian"
TEST_STAGE_DIR="$PACKAGE_OUTPUT_DIR/test"
REPO_BINARIES_DIR="$REPOROOT/artifacts/ubuntu.14.04-x64/stage2"
MANPAGE_DIR="$REPOROOT/Documentation/manpages"

NIGHTLY_PACKAGE_NAME="dotnet-nightly"
RELEASE_PACKAGE_NAME="dotnet"

[ -z "$CHANNEL" ] && CHANNEL="dev"

execute_build(){
    determine_package_name
    create_empty_debian_layout
    copy_files_to_debian_layout
    create_debian_package
}

determine_package_name(){
    if [[ "$RELEASE_SUFFIX" == "dev" ]]; then
        DOTNET_DEB_PACKAGE_NAME=$NIGHTLY_PACKAGE_NAME
    elif [[ "beta rc1 rc2 rtm" =~ (^| )"$RELEASE_SUFFIX"($| ) ]]; then
        DOTNET_DEB_PACKAGE_NAME=$RELEASE_PACKAGE_NAME
    elif [[ "$RELEASE_SUFFIX" == "" ]]; then
        DOTNET_DEB_PACKAGE_NAME=$RELEASE_PACKAGE_NAME
    else
        DOTNET_DEB_PACKAGE_NAME=$NIGHTLY_PACKAGE_NAME
    fi
}

execute_test(){
    test_debian_package
}

create_empty_debian_layout(){
    header "Creating empty debian package layout"

    rm -rf "$PACKAGE_LAYOUT_DIR"
    mkdir -p "$PACKAGE_LAYOUT_DIR"

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
    cp "$PACKAGING_ROOT/$DOTNET_DEB_PACKAGE_NAME-debian_config.json" "$PACKAGE_LAYOUT_DIR/debian_config.json"

    # Copy Manpages
    cp -a "$MANPAGE_DIR/." "$PACKAGE_LAYOUT_DIR/docs"
}

create_debian_package(){
    header "Packing .deb"

    mkdir -p "$PACKAGE_OUTPUT_DIR"
    
    "$PACKAGING_TOOL_DIR/package_tool" -i "$PACKAGE_LAYOUT_DIR" -o "$PACKAGE_OUTPUT_DIR" -v $DOTNET_CLI_VERSION -n $DOTNET_DEB_PACKAGE_NAME
}

test_debian_package(){
    header "Testing debian package"
    
    install_bats
    run_package_integrity_tests

    install_debian_package
    run_e2e_test
    remove_debian_package
}

install_bats() {
    rm -rf $TEST_STAGE_DIR
    git clone https://github.com/sstephenson/bats.git $TEST_STAGE_DIR
}

install_debian_package() {
    sudo dpkg -i $DEBIAN_FILE
}

remove_debian_package() {
    sudo dpkg -r $DOTNET_DEB_PACKAGE_NAME
}

run_package_integrity_tests() {
    # Set LAST_VERSION_URL to enable upgrade tests
    export LAST_VERSION_URL="https://dotnetcli.blob.core.windows.net/dotnet/$CHANNEL/Installers/Latest/dotnet-ubuntu-x64.latest.deb"

    $TEST_STAGE_DIR/bin/bats $PACKAGE_OUTPUT_DIR/test_package.bats
}

run_e2e_test(){
    local dotnet_path="/usr/bin/dotnet"

    header "Running EndToEnd Tests against debian package using ${dotnet_path}"
    
    # Won't affect outer functions
    cd $REPOROOT/test/EndToEnd
    $dotnet_path build
    $dotnet_path test -xml $TEST_STAGE_DIR/debian-endtoend-testResults.xml
}

execute_build

DEBIAN_FILE=$(find $PACKAGE_OUTPUT_DIR -iname "*.deb")

execute_test

# Publish
$REPOROOT/scripts/publish/publish.sh $DEBIAN_FILE 
