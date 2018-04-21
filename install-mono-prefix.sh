#!/bin/bash
set -e
if [ $# -ne 1 ]; then
       echo "Usage: $0 </path/to/mono/installation>"
       exit 1
fi

if [ -d "artifacts/2/Release-MONO" ]; then
    CONFIG=Release-MONO
elif [ -d "artifacts/2/Debug-MONO" ]; then
    CONFIG=Debug-MONO
else
    echo "Error: No build directory 'artifacts/2/Release-MONO' or 'artifacts/2/Debug-MONO' found."
    exit 1
fi

MONO_PREFIX=$1

msbuild mono/build/install.proj /p:MonoInstallPrefix=$MONO_PREFIX /p:Configuration=$CONFIG
