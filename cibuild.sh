#!/bin/sh

set -e

usage()
{
    echo "Options"
    echo "  --scope <scope>                Scope of the build (Compile / Test)"
    echo "  --target <target>              CoreCLR or Mono (default: CoreCLR)"
    echo "  --host <host>                  CoreCLR or Mono (default: Mono)"
}

setHome()
{
    if [ -z ${HOME+x} ]
    then
        BUILD_COMMAND="( export HOME=$HOME_DEFAULT ; $BUILD_COMMAND )"
        INIT_BUILD_TOOLS_COMMAND="( export HOME=$HOME_DEFAULT ; $INIT_BUILD_TOOLS_COMMAND )"
        mkdir -p $HOME_DEFAULT
    fi
}

downloadMSBuildForMono()
{
    if [ ! -e "$MSBUILD_EXE" ]
    then
        mkdir -p $PACKAGES_DIR # Create packages dir if it doesn't exist.

        echo "Downloading MSBUILD from $MSBUILD_DOWNLOAD_URL"
        curl -sL -o $MSBUILD_ZIP "$MSBUILD_DOWNLOAD_URL"

        unzip -q $MSBUILD_ZIP -d $PACKAGES_DIR
        find "$PACKAGES_DIR/mono-msbuild/bin/Unix/Debug-MONO" -name "*.exe" -exec chmod "+x" '{}' ';'
        rm $MSBUILD_ZIP
    fi
}

build()
{
	echo Build Command: "$BUILD_COMMAND"

    eval "$BUILD_COMMAND"

	echo Build completed. Exit code: $?
	egrep "Warning\(s\)|Error\(s\)|Time Elapsed" "$LOG_PATH_ARG"
	echo "Log: $LOG_PATH_ARG"
}

setMonoDir(){
    if [[ "$MONO_BIN_DIR" = "" ]]; then
                MONO_BIN_DIR=`dirname \`which mono\``
                MONO_BIN_DIR=${MONO_BIN_DIR}/
    fi
}

# Paths
THIS_SCRIPT_PATH="`dirname \"$0\"`"
PACKAGES_DIR="$THIS_SCRIPT_PATH/packages"
TOOLS_DIR="$THIS_SCRIPT_PATH/Tools"
MSBUILD_DOWNLOAD_URL="https://github.com/Microsoft/msbuild/releases/download/mono-hosted-msbuild-v0.1/mono-msbuild.zip"
MSBUILD_ZIP="$PACKAGES_DIR/msbuild.zip"
HOME_DEFAULT="/tmp/msbuild-CI-home"

LOG_PATH_ARG="$THIS_SCRIPT_PATH"/"msbuild.log"
PROJECT_FILE_ARG="$THIS_SCRIPT_PATH"/"build.proj"

# Default build arguments
TARGET_ARG="Build"

#parse command line args
while [ $# -gt 0 ]
do
    opt="$1"
    case $opt in
        -h|--help)
        usage
        exit 1
        ;;

        --scope)
        SCOPE=$2
        shift 2
        ;;

        --target)
        target=$2
        shift 2
        ;;

        --host)
        host=$2
        shift 2
        ;;

        *)
        usage
        exit 1
        ;;
    esac
done

# determine OS
OS_NAME=$(uname -s)
case $OS_NAME in
    Darwin)
        OS_ARG="OSX"
        ;;

    Linux)
        OS_ARG="Unix"
        ;;

    *)
        echo "Unsupported OS $OS_NAME detected, configuring as if for Linux"
        OS_ARG="Unix"
        ;;
esac

if [ "$SCOPE" = "Compile" ]; then
	TARGET_ARG="Build"
elif [ "$SCOPE" = "Test" ]; then
	TARGET_ARG="BuildAndTest"
fi

# Determine configuration
case $target in
    CoreCLR)
        CONFIGURATION=Debug-NetCore
        ;;

    Mono)
        setMonoDir
        CONFIGURATION=Debug-MONO
        EXTRA_ARGS="/p:CscToolExe=mcs /p:CscToolPath=$MONO_BIN_DIR"
        RUNTIME_HOST_ARGS="--debug"
        ;;
    *)
        echo "Unsupported target detected: $target. Configuring as if for CoreCLR"
        CONFIGURATION=Debug-NetCore
        ;;
esac

# Determine runtime host
case $host in
    CoreCLR)
        RUNTIME_HOST="$TOOLS_DIR/corerun"
        RUNTIME_HOST_ARGS=""
        MSBUILD_EXE="$TOOLS_DIR/MSBuild.exe"
        ;;

    Mono)
        setMonoDir
        RUNTIME_HOST="${MONO_BIN_DIR}mono"
        MSBUILD_EXE="$PACKAGES_DIR/mono-msbuild/bin/Unix/Debug-MONO/MSBuild.exe"

        downloadMSBuildForMono
        ;;
    *)
        echo "Unsupported host detected: $host. Configuring as if for Mono"
# TODO: set this back to .net core when build tools updates msbuild to new version 
        setMonoDir
        RUNTIME_HOST="${MONO_BIN_DIR}mono"
        MSBUILD_EXE="$PACKAGES_DIR/mono-msbuild/bin/Unix/Debug-MONO/MSBuild.exe"

        downloadMSBuildForMono
        ;;
esac

MSBUILD_ARGS="$PROJECT_FILE_ARG /t:$TARGET_ARG /p:OS=$OS_ARG /p:Configuration=$CONFIGURATION /verbosity:minimal $EXTRA_ARGS /fl "' "'"/flp:v=diag;logfile=$LOG_PATH_ARG"'"'

BUILD_COMMAND="$RUNTIME_HOST $RUNTIME_HOST_ARGS $MSBUILD_EXE $MSBUILD_ARGS"

INIT_BUILD_TOOLS_COMMAND="$THIS_SCRIPT_PATH/init-tools.sh"

#home is not defined on CI machines
setHome

#restore build tools
eval "$INIT_BUILD_TOOLS_COMMAND"

build
