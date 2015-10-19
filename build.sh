#!/usr/bin/env bash
# 
# Build Script
# Currently Intended to build on ubuntu14.04

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

REPO_ROOT=$DIR

OUTPUT_DIR="$REPO_ROOT/bin"
PACKAGE_LAYOUT_DIR="$OUTPUT_DIR/package_layout"
PACKAGE_OUTPUT_DIR="$OUTPUT_DIR/package_output"
REPO_BINARIES_DIR="$REPO_ROOT/artifacts/ubuntu.14.04-x64"

execute(){
	build_repo
	create_empty_debian_layout
	copy_files_to_debian_layout
	create_debian_package
}

acquire_previous_version(){

}

build_repo(){

}

create_empty_debian_layout(){
	rm -rf $PACKAGE_LAYOUT_DIR
	mkdir -p $PACKAGE_LAYOUT_DIR

	mkdir "$PACKAGE_LAYOUT_DIR/\$"
	mkdir "$PACKAGE_LAYOUT_DIR/package_root"
	mkdir "$PACKAGE_LAYOUT_DIR/samples"
	mkdir "$PACKAGE_LAYOUT_DIR/docs"
}

copy_files_to_debian_layout(){
	# Copy Built Binaries
	cp -a "$REPO_BINARIES_DIR/." "$PACKAGE_LAYOUT_DIR/package_root"

	# Copy config file
	cp "$REPO_ROOT/debian_config.json" "$PACKAGE_LAYOUT_DIR"

	# Copy Wrapper Scripts
	# TODO: This is a workaround for https://github.com/dotnet/coreclr/issues/1774
	cp "$REPO_ROOT/wrappers/." "PACKAGE_LAYOUT_DIR/package_root"
}

create_debian_package(){
	$DIR/package_tool/package_tool $PACKAGE_LAYOUT_DIR $PACKAGE_OUTPUT_DIR
}