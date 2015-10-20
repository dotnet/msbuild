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

UNAME=$(uname)

  
if [ "$UNAME" == "Darwin" ]; then
    echo "Building Currently not supported on OSX"
    exit 1
fi


REPO_ROOT=$DIR

OUTPUT_DIR="$REPO_ROOT/bin"
PACKAGE_LAYOUT_DIR="$OUTPUT_DIR/package_layout"
PACKAGE_OUTPUT_DIR="$OUTPUT_DIR/package_output"
REPO_BINARIES_DIR="$REPO_ROOT/bin/$UNAME"

execute(){
	install_dotnet
	build_repo
	create_empty_debian_layout
	copy_files_to_debian_layout
	create_debian_package
}

install_dotnet(){
	sudo sh -c 'echo "deb [arch=amd64] http://tux-devrepo.corp.microsoft.com/repos/dotnet-dev/ trusty main" > /etc/apt/sources.list.d/dotnetdev.list'
	sudo apt-key adv --keyserver tux-devrepo.corp.microsoft.com --recv-keys 008C0E6C
	sudo apt-get update


	# install/upgrade dotnet debian package
	echo "Installing dotnet.."

	sudo apt-get install dotnet

	if [[ $? > 0 ]]
	then
	    echo "Installing 'dotnet' failed, exiting."
	    exit 1
	else
	    echo "Installing 'dotnet' succeeded."
	fi
}

build_repo(){
	if [ -z "$RID" ]; then
	    if [ "$UNAME" == "Darwin" ]; then
	        RID=osx.10.10-x64
	    elif [ "$UNAME" == "Linux" ]; then
	        # Detect Distro?
	        RID=ubuntu.14.04-x64
	    else
	        echo "Unknown OS: $UNAME" 1>&2
	        exit 1
	    fi
	fi

	dotnet restore "$REPO_ROOT" --runtime osx.10.10-x64 --runtime ubuntu.14.04-x64 --runtime osx.10.11-x64

	dirs=(`find ./src -type f -iname 'project.json' | sed -r 's|/[^/]+$||' |sort |uniq`)

	for i in ${dirs[@]}; do
	    echo "Building directory $i";
	    pushd $i
	    # HACK for getting dnu. Right now dotnet-restore depends on dnu.
	    # Restore the dnx package into tmp and copy the binaries into the output folder
	    if [[ $i == *"Microsoft.DotNet.Tools.Restore"* ]]; then
			dotnet restore . --runtime $RID --packages /tmp
			mkdir $REPO_BINARIES_DIR/dnx
			cp -r /tmp/dnx-coreclr-linux-x64/1.0.0-*/bin/* "$REPO_BINARIES_DIR/dnx/"
			cp "$REPO_ROOT/scripts/dotnet-restore" "$REPO_BINARIES_DIR"
	    elif [[ $i == *"Microsoft.Extensions.ProjectModel"* ]]; then
			:
			#HACK to prevent publishing of Microsoft.Extensions.ProjectModel
	    else
			dotnet-publish --framework dnxcore50 --runtime $RID --output "$REPO_BINARIES_DIR" .
	    fi
	    popd
	done
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
}

create_debian_package(){
	$DIR/package_tool/package_tool $PACKAGE_LAYOUT_DIR $PACKAGE_OUTPUT_DIR
}

execute
