#!/bin/bash
set -e
if [ $# -lt 1 ]; then
       echo "Usage: $0 </path/to/mono/installation> [<extra msbuild args>]"
       exit 1
fi

BOOTSTRAP_DIR_PREFIX="artifacts/bin/MSBuild.Bootstrap/"

if [ -d "$BOOTSTRAP_DIR_PREFIX/Release-MONO" ]; then
    CONFIG=Release-MONO
elif [ -d "$BOOTSTRAP_DIR_PREFIX/Debug-MONO" ]; then
    CONFIG=Debug-MONO
else
    echo "Error: No build directory '$BOOTSTRAP_DIR_PREFIX/Release-MONO' or '$BOOTSTRAP_DIR_PREFIX/Debug-MONO' found."
    exit 1
fi

MONO_PREFIX=$1
shift

msbuild mono/build/install.proj /p:MonoInstallPrefix=$MONO_PREFIX /p:Configuration=$CONFIG "$@"
