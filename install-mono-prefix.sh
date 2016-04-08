#!/bin/sh
if [ $# -ne 1 ]; then
	echo "Usage: install-mono.sh </path/to/mono/installation>"
	exit 1
fi

MSBUILD_VERSION=15.0
MONO_PREFIX=$1
MSBUILD_OUT_DIR="bin/Debug-MONO/OSX_Deployment"
MSBUILD_INSTALL_BIN_DIR="$MONO_PREFIX/lib/mono/msbuild/${MSBUILD_VERSION}/bin"

mkdir -p $MSBUILD_INSTALL_BIN_DIR
cp -r $MSBUILD_OUT_DIR/ $MSBUILD_INSTALL_BIN_DIR
rm $MSBUILD_INSTALL_BIN_DIR/*UnitTests*
rm $MSBUILD_INSTALL_BIN_DIR/*xunit*

cp msbuild-mono-deploy.in $MONO_PREFIX/bin/msbuild
