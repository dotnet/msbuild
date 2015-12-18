#!/bin/sh

set -e

usage()
{
    echo "Options"
    echo "  --scope <scope>                Scope of the build (Compile / Test)"
}

setHome(){
    if [ -z ${HOME+x} ]
    then
        MONO_COMMAND="( export HOME=$HOME_DEFAULT ; $MONO_COMMAND )"
        mkdir -p $HOME_DEFAULT
    fi
}

downloadMSBuild(){
    if [ ! -e "$MSBUILD_EXE" ]
    then
        mkdir -p $PACKAGES_DIR # Create packages dir if it doesn't exist.

        echo "Downloading MSBUILD from $MSBUILD_DOWNLOAD_URL"
        curl -sL -o $MSBUILD_ZIP "$MSBUILD_DOWNLOAD_URL"

        unzip -q $MSBUILD_ZIP -d $PACKAGES_DIR
        rm $MSBUILD_ZIP
    fi
}

build()
{
	echo Build Command: "$MONO_COMMAND"

  eval "$MONO_COMMAND"

	echo Build completed. Exit code: $?
	egrep "Warning\(s\)|Error\(s\)|Time Elapsed" "$LOG_PATH_ARG"
	echo "Log: $LOG_PATH_ARG"
}

THIS_SCRIPT_PATH="`dirname \"$0\"`"
PACKAGES_DIR="$THIS_SCRIPT_PATH/packages"
MSBUILD_EXE="$PACKAGES_DIR/mono-msbuild/bin/Unix/Debug-MONO/MSBuild.exe"
MSBUILD_DOWNLOAD_URL="https://github.com/Microsoft/msbuild/releases/download/mono-hosted-msbuild-v0.1/mono-msbuild.zip"
MSBUILD_ZIP="$PACKAGES_DIR/msbuild.zip"
HOME_DEFAULT="/tmp/msbuild-CI-home"


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

MONO_COMMAND="mono $MSBUILD_EXE $MSBUILD_ARGS"

#home is not defined on CI machines
setHome

downloadMSBuild

build
