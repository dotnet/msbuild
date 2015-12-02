#!/bin/sh

set -e

usage()
{
    echo "Options"
    echo "  --scope <scope>                Scope of the build (Compile / Test)"
}

downloadMSBuild(){
    if [ ! -e "$MSBUILD_EXE" ]
    then
        mkdir -p $PACKAGES_DIR # Create packages dir if it doesn't exist.
        echo "Downloading MSBUILD from $MSBUILD_DOWNLOAD_URL"
        curl -sL "$MSBUILD_DOWNLOAD_URL" | tar xz -C "$PACKAGES_DIR"
    fi
}

build()
{
	echo Build Command: "mono $MONO_ARGS"

	mono $MONO_ARGS

	echo Build completed. Exit code: $?
	egrep "Warning\(s\)|Error\(s\)|Time Elapsed" "$LOG_PATH_ARG"
	echo "Log: $LOG_PATH_ARG"
}

THIS_SCRIPT_PATH="`dirname \"$0\"`"
PACKAGES_DIR="$THIS_SCRIPT_PATH/packages"
MSBUILD_EXE="$PACKAGES_DIR/mono-msbuild/bin/Unix/Debug-MONO/MSBuild.exe"
MSBUILD_DOWNLOAD_URL="https://github.com/Microsoft/msbuild/releases/download/mono-hosted-msbuild-v0.1/mono-msbuild.zip"

#Default build arguments
TARGET_ARG="Build"
LOG_PATH_ARG="$THIS_SCRIPT_PATH"/"msbuild.log"
PROJECT_FILE_ARG="$THIS_SCRIPT_PATH"/"build.proj"

#parse command line args
while [[ $# > 0 ]]
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

        *)
        usage
        exit 1
        ;;
    esac
done

#determine OS
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

if [[ "$SCOPE" = "Compile" ]]; then
	TARGET_ARG="Build"
elif [[ "$SCOPE" = "Test" ]]; then
	TARGET_ARG="BuildAndTest"
fi

MSBUILD_ARGS="$PROJECT_FILE_ARG /t:$TARGET_ARG /p:OS=$OS_ARG /p:Configuration=Debug-Netcore /verbosity:minimal"' "'"/fileloggerparameters:Verbosity=diag;LogFile=$LOG_PATH_ARG"'"'

MONO_ARGS="$MSBUILD_EXE $MSBUILD_ARGS"

downloadMSBuild

build
