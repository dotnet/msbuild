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
REPOROOT="$DIR/../.."

help(){
    echo "Usage: $0"
    echo ""
    echo "Options:"
    echo "  --version <version>                Specify a version for the package."
    echo "  --input <input directory>          Package the entire contents of the directory tree."
    echo "  --manpages <man pages directory>   Directory containing man pages for the package (Optional)."
    echo "  --output <output debfile>          The full path to which the package will be written."
    echo "  --package-name <package name>      Package to identify during installation. Example - 'dotnet-nightly', 'dotnet'"
    echo "  --previous-version-url <url>           Url to the previous version of the debian packge against which to run the upgrade tests."
    exit 1
}

parseargs(){

    while [[ $# > 0 ]]; do
        lowerI="$(echo $1 | awk '{print tolower($0)}')"
        case $lowerI in
        -m|--manpages)
            MANPAGE_DIR=$2
            shift
            ;;
        -o|--output)
            OUTPUT_DEBIAN_FILE=$2
            shift
            ;;
        -i|--input)
            REPO_BINARIES_DIR=$2
            shift
            ;;
        -p|--package-name)
            DOTNET_DEB_PACKAGE_NAME=$2
            shift
            ;;
        -v|--version)
            DOTNET_CLI_VERSION=$2
            shift
            ;;
        --previous-version-url)
            PREVIOUS_VERSION_URL=$2
            shift
            ;;
        --help)
            help
            ;;
        *)
            break
            ;;
        esac
        shift
    done

    if [ -z "$DOTNET_CLI_VERSION" ]; then
        echo "Provide a version number. Missing option '--version'" && help
    fi

    if [ -z "$OUTPUT_DEBIAN_FILE" ]; then
        echo "Provide an output deb. Missing option '--output'" && help
    fi

    if [ -z "$REPO_BINARIES_DIR" ]; then
        echo "Provide an input directory. Missing option '--input'" && help
    fi

    if [ -z "$DOTNET_DEB_PACKAGE_NAME" ]; then
        echo "Provide an the name for the debian package. Missing option '--package-name'" && help
    fi

    if [ -z "$PREVIOUS_VERSION_URL" ]; then
        echo "Provide a URL to the previous debian pacakge (Required for running upgrade tests). Missing option '--previous-version-url'" && help
    fi

    if [ ! -d "$REPO_BINARIES_DIR" ]; then
        echo "'$REPO_BINARIES_DIR' - is either missing or not a directory" 1>&2
        exit 1
    fi

}

parseargs $@

PACKAGING_ROOT="$REPOROOT/packaging/debian"
PACKAGING_TOOL_DIR="$REPOROOT/tools/DebianPackageTool"

PACKAGE_OUTPUT_DIR=$(dirname "${OUTPUT_DEBIAN_FILE}")
PACKAGE_LAYOUT_DIR="$PACKAGE_OUTPUT_DIR/deb_intermediate"
TEST_STAGE_DIR="$PACKAGE_OUTPUT_DIR/debian_tests"

# remove any residual deb files from earlier builds
rm -f "$PACKAGE_OUTPUT_DIR/*.deb"

execute_build(){
    create_empty_debian_layout
    copy_files_to_debian_layout
    create_debian_package
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
    export LAST_VERSION_URL="$PREVIOUS_VERSION_URL"

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

mv -f "$DEBIAN_FILE" "$OUTPUT_DEBIAN_FILE"
