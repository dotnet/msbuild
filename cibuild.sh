#!/usr/bin/env bash

set -e

usage()
{
    echo "Options"
    echo "  --scope <scope>                Scope of the build (Compile / Test)"
    echo "  --target <target>              CoreCLR or Mono (default: CoreCLR)"
    echo "  --host <host>                  CoreCLR or Mono (default: CoreCLR)"
    echo "  --bootstrap-only               Build and bootstrap MSBuild but do not build again with those binaries"
    echo "  --build-only                   Only build using a downloaded copy of MSBuild but do not bootstrap"
    echo "                                 or build again with those binaries"
    echo "  --config                       Debug or Release configuration (default: Debug)"
}

restoreBuildTools(){
    eval "$THIS_SCRIPT_PATH/init-tools.sh"
}

# home is not defined on CI machines
setHome()
{
    if [ -z "${HOME+x}" ]
    then
        export HOME=$HOME_DEFAULT
        mkdir -p "$HOME_DEFAULT"

        # Use a different temp directory in CI so that hopefully things are a little more stable
        export TMPDIR=$TEMP_DEFAULT
        mkdir -p "$TEMP_DEFAULT"
    fi
}

downloadMSBuildForMono()
{
    if [ ! -e "$MSBUILD_EXE" ]
    then
        mkdir -p "$PACKAGES_DIR" # Create packages dir if it doesn't exist.

        echo "** Downloading MSBUILD from $MSBUILD_DOWNLOAD_URL"
        curl -sL -o "$MSBUILD_ZIP" "$MSBUILD_DOWNLOAD_URL"

        unzip -q "$MSBUILD_ZIP" -d "$PACKAGES_DIR"
        find "$PACKAGES_DIR/msbuild" -name "*.exe" -exec chmod "+x" '{}' ';'
        rm "$MSBUILD_ZIP"
    fi
}

runMSBuildWith()
{
    local runtimeHost=$1
    local runtimeHostArgs=$2
    local msbuildExe=$3
    local msbuildArgs=$4
    local logPath=$5

    local buildCommand="$runtimeHost $runtimeHostArgs $msbuildExe $msbuildArgs"" /fl "'"'"/flp:v=diag;logfile=$logPath"'"'

    echo
    echo "** using MSBuild in path: $msbuildExe"
    echo "** using runtime host in path: $runtimeHost"
    echo "** $buildCommand"
    eval "$buildCommand"

    echo
    echo "** Build completed. Exit code: $?"
    grep -E "Warning\(s\)|Error\(s\)|Time Elapsed" "$logPath"
    echo "** Log: $logPath"
}

setMonoDir(){
    if [[ "$MONO_BIN_DIR" = "" ]]; then
                MONO_BIN_DIR=$(dirname "$(which mono)")
                MONO_BIN_DIR=${MONO_BIN_DIR}/
    fi
}

# this function is copied from init-tools.sh

get_current_linux_name() {
    # Detect Distro
    if [ "$(cat /etc/*-release | grep -cim1 ubuntu)" -eq 1 ]; then
        if [ "$(cat /etc/*-release | grep -cim1 14.04)" -eq 1 ]; then
            echo "ubuntu.14.04"
            return 0
        fi
        if [ "$(cat /etc/*-release | grep -cim1 16.04)" -eq 1 ]; then
            echo "ubuntu.16.04"
            return 0
        fi
        if [ "$(cat /etc/*-release | grep -cim1 16.10)" -eq 1 ]; then
            echo "ubuntu.16.10"
            return 0
        fi

        echo "ubuntu"
        return 0
    elif [ "$(cat /etc/*-release | grep -cim1 centos)" -eq 1 ]; then
        echo "centos"
        return 0
    elif [ "$(cat /etc/*-release | grep -cim1 rhel)" -eq 1 ]; then
        if [ "$(grep -cim1 'VERSION_ID="7\.' /etc/os-release)" -eq 1 ]; then
            echo "rhel.7"
            return 0
        fi
        echo "rhel"
        return 0
    elif [ "$(cat /etc/*-release | grep -cim1 debian)" -eq 1 ]; then
        echo "debian"
        return 0
    elif [ "$(cat /etc/*-release | grep -cim1 fedora)" -eq 1 ]; then
        if [ "$(cat /etc/*-release | grep -cim1 23)" -eq 1 ]; then
            echo "fedora.23"
            return 0
        fi
        if [ "$(cat /etc/*-release | grep -cim1 24)" -eq 1 ]; then
            echo "fedora.24"
            return 0
        fi
    elif [ "$(cat /etc/*-release | grep -cim1 opensuse)" -eq 1 ]; then
        if [ "$(cat /etc/*-release | grep -cim1 13.2)" -eq 1 ]; then
            echo "opensuse.13.2"
            return 0
        fi
        if [ "$(cat /etc/*-release | grep -cim1 42.1)" -eq 1 ]; then
            echo "opensuse.42.1"
            return 0
        fi
    fi

    # Cannot determine Linux distribution, assuming Ubuntu 14.04.
    echo "ubuntu"
    return 0
}

BOOTSTRAP_ONLY=false

# Paths
THIS_SCRIPT_PATH="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PACKAGES_DIR="$THIS_SCRIPT_PATH/packages"
TOOLS_DIR="$THIS_SCRIPT_PATH/Tools"
MSBUILD_DOWNLOAD_URL="https://github.com/Microsoft/msbuild/releases/download/mono-hosted-msbuild-v0.03/mono_msbuild_d25dd923839404bd64cc63f420e75acf96fc75c4.zip"
MSBUILD_ZIP="$PACKAGES_DIR/msbuild.zip"
HOME_DEFAULT="$WORKSPACE/msbuild-CI-home"
TEMP_DEFAULT="$WORKSPACE/tmp"

PROJECT_FILE_ARG='"'"$THIS_SCRIPT_PATH/build.proj"'"'
BOOTSTRAP_FILE_ARG='"'"$THIS_SCRIPT_PATH/targets/BootStrapMSBuild.proj"'"'

# Default msbuild arguments
TARGET_ARG="Build"
EXTRA_ARGS=""
CSC_ARGS=""
PROJECT_CONFIG=Debug

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

        --build-only)
        BUILD_ONLY=true
        shift 1
        ;;

        --bootstrap-only)
        BOOTSTRAP_ONLY=true
        shift 1
        ;;

        --config)
        PROJECT_CONFIG=$2
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
        EXTRA_ARGS="$EXTRA_ARGS /p:RuntimeSystem=$(get_current_linux_name)"
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

# If unspecified, default
if [ "$target" = "" ]; then
    target=CoreCLR
fi

if [ "$host" = "Mono" ]; then
    # check if mono is available
    echo "debug: which mono: $(which mono)"
    echo "MONO_BIN_DIR: $MONO_BIN_DIR"
    if [ "$(which mono)" = "" ] && [ "$MONO_BIN_DIR" = "" ]; then
        echo "** Error: Building with host Mono, requires Mono to be installed."
        exit 1
    fi
fi

case $PROJECT_CONFIG in
    Debug)
        CONFIGURATION=Debug
        ;;
    Release)
        CONFIGURATION=Release
        ;;
    *)
        echo "Unknown configuration $PROJECT_CONFIG. Defaulting to Debug"
        ;;
esac

case $target in
    CoreCLR)
        CONFIGURATION=${PROJECT_CONFIG}-NetCore
        MSBUILD_BOOTSTRAPPED_EXE='"'"$THIS_SCRIPT_PATH/bin/Bootstrap-NetCore/MSBuild.dll"'"'
        ;;

    Mono)
        setMonoDir
        CONFIGURATION=${PROJECT_CONFIG}-MONO
        RUNTIME_HOST_ARGS="--debug"
        MSBUILD_BOOTSTRAPPED_EXE='"'"$THIS_SCRIPT_PATH/bin/Bootstrap/MSBuild.dll"'"'
        ;;
    *)
        echo "Unsupported target detected: $target. Aborting."
        usage
        exit 1
        ;;
esac

# Determine runtime host

# If no host was specified, default to the one that makes sense for
# the selected target.
if [ "$host" = "" ]; then
    host=$target
fi

case $host in
    CoreCLR)
        RUNTIME_HOST="$TOOLS_DIR/dotnetcli/dotnet"
        RUNTIME_HOST_ARGS=""
        MSBUILD_EXE="$TOOLS_DIR/MSBuild.exe"
        EXTRA_ARGS="$EXTRA_ARGS /m"
        ;;

    Mono)
        setMonoDir
        RUNTIME_HOST="${MONO_BIN_DIR}mono"
        MSBUILD_EXE="$PACKAGES_DIR/msbuild/MSBuild.exe"
        CSC_ARGS="/p:CscToolExe=csc.exe /p:CscToolPath=$PACKAGES_DIR/msbuild/ /p:DebugType=portable"

        if [[ "$MONO_BIN_DIR" != "" ]]; then
            echo "** Using mono from $RUNTIME_HOST"
            $RUNTIME_HOST --version
        fi

        downloadMSBuildForMono
        ;;
    *)
        echo "Unsupported host detected: $host. Aborting."
        usage
        exit 1
        ;;
esac

BOOTSTRAP_BUILD_LOG_PATH="$THIS_SCRIPT_PATH"/msbuild_bootstrap_build-"$host".log
LOCAL_BUILD_LOG_PATH="$THIS_SCRIPT_PATH"/msbuild_local_build-"$host".log
LOCAL_BUILD_BINLOG_PATH="$THIS_SCRIPT_PATH"/msbuild_rebuild-"$host".binlog
MOVE_LOG_PATH="$THIS_SCRIPT_PATH"/msbuild_move_bootstrap-"$host".log

BUILD_MSBUILD_ARGS="$PROJECT_FILE_ARG /p:OS=$OS_ARG /p:Configuration=$CONFIGURATION /p:OverrideToolHost=$RUNTIME_HOST /verbosity:minimal $EXTRA_ARGS"

setHome

restoreBuildTools

echo
echo "** Rebuilding MSBuild with downloaded binaries"
runMSBuildWith "$RUNTIME_HOST" "$RUNTIME_HOST_ARGS" "$MSBUILD_EXE" "/t:Rebuild $BUILD_MSBUILD_ARGS $CSC_ARGS" "$BOOTSTRAP_BUILD_LOG_PATH"

if [[ $BUILD_ONLY = true ]]; then
    exit $?
fi

echo
echo "** Moving bootstrapped MSBuild to the bootstrap folder"
MOVE_MSBUILD_ARGS="$BOOTSTRAP_FILE_ARG /p:OS=$OS_ARG /p:Configuration=$CONFIGURATION /p:OverrideToolHost=$RUNTIME_HOST /verbosity:minimal"
runMSBuildWith "$RUNTIME_HOST" "$RUNTIME_HOST_ARGS" "$MSBUILD_EXE" "$MOVE_MSBUILD_ARGS" "$MOVE_LOG_PATH"

if [[ $BOOTSTRAP_ONLY = true ]]; then
    exit $?
fi

# Microsoft.Net.Compilers package is available now, so we can use the latest csc.exe
if [ "$host" = "Mono" ]; then
        CSC_EXE="$PACKAGES_DIR/microsoft.net.compilers/2.0.0-rc3-61110-06/tools/csc.exe"
        CSC_ARGS="/p:CscToolExe=csc.exe /p:CscToolPath=$(dirname "$CSC_EXE") /p:DebugType=portable"
fi

# The set of warnings to suppress for now
# warning MSB3276: Found conflicts between different versions of the same dependent assembly.
#   - Microsoft.VisualStudio.Setup.Configuration.Interop.dll (referenced from Utilities project) references mscorlib 2.0 and thus conflicting
#     with mscorlib 4.0
# warning MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
# warning MSB3026: Could not copy "XXX" to "XXX". Beginning retry 1 in 1000ms.
# warning AL1053: The version '1.2.3.4-foo' specified for the 'product version' is not in the normal 'major.minor.build.revision' format
_NOWARN="MSB3276;MSB3277;MSB3026;AL1053"

echo
echo "** Rebuilding MSBuild with locally built binaries"
runMSBuildWith "$RUNTIME_HOST" "$RUNTIME_HOST_ARGS" "$MSBUILD_BOOTSTRAPPED_EXE" "/t:$TARGET_ARG $BUILD_MSBUILD_ARGS $CSC_ARGS /warnaserror /nowarn:\"$_NOWARN\"" "$LOCAL_BUILD_LOG_PATH" /bl:"$LOCAL_BUILD_BINLOG_PATH"
