#!/bin/sh
if [ $# -ne 1 ]; then
	echo "Usage: install-mono.sh </path/to/mono/installation>"
	exit 1
fi

MSBUILD_TOOLSVERSION=15.0
MONO_PREFIX=$1
MSBUILD_INSTALL_BIN_DIR="$MONO_PREFIX/lib/mono/msbuild/${MSBUILD_TOOLSVERSION}/bin"
XBUILD_DIR=$MONO_PREFIX/lib/mono/xbuild
MSBUILD_OUT_DIR="bin/Release-MONO/*_Deployment"

if [ ! -d "$MSBUILD_OUT_DIR" ]; then
    MSBUILD_OUT_DIR="bin/Debug-MONO/*_Deployment"
fi

if [ -d "bin/Release-MONO" ]; then
    MSBUILD_OUT_DIR="bin/Release-MONO/*_Deployment"
elif [ -d "bin/Debug-MONO" ]; then
    MSBUILD_OUT_DIR="bin/Debug-MONO/*_Deployment"
else
    echo "Error: No bin directory 'bin/Release-MONO' or 'bin/Debug-MONO' found."
    exit 1
fi

mkdir -p ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}
mkdir -p ${DESTDIR}${XBUILD_DIR}/$MSBUILD_TOOLSVERSION
mkdir -p ${DESTDIR}${MONO_PREFIX}/bin

cp -r $MSBUILD_OUT_DIR/* ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}

# Deploy files meant for the default $(MSBuildExtensionsPath)
cp -r mono/ExtensionsPath/ ${DESTDIR}${XBUILD_DIR}
cp -r mono/ExtensionsPath-ToolsVersion/ ${DESTDIR}${XBUILD_DIR}/${MSBUILD_TOOLSVERSION}

mv ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/Microsoft.Common.props ${DESTDIR}${XBUILD_DIR}/$MSBUILD_TOOLSVERSION
mv ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/Microsoft.VisualStudioVersion.v* ${DESTDIR}${XBUILD_DIR}/$MSBUILD_TOOLSVERSION

rm ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/*UnitTests*
rm ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/*xunit*
rm ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/NuGet*
rm ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/System.Runtime.InteropServices.RuntimeInformation.dll
rm ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/Roslyn/csc.exe*

cp ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/Roslyn/System.Reflection.Metadata.dll ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}

cp -R ${DESTDIR}${XBUILD_DIR}/14.0/Imports ${DESTDIR}${XBUILD_DIR}/${MSBUILD_TOOLSVERSION}

cp -R nuget-support/tv/* ${DESTDIR}${XBUILD_DIR}/$MSBUILD_TOOLSVERSION
cp -R nuget-support/tasks-targets/* ${DESTDIR}${XBUILD_DIR}/

for f in ${XBUILD_DIR}/Microsoft/NuGet/*; do ln -s $f ${DESTDIR}${XBUILD_DIR} ; done

# copy SDKs
SDKS_SRC_DIR=sdks
SDKS_OUT_DIR=${MSBUILD_INSTALL_BIN_DIR}/Sdks

cp -R ${SDKS_SRC_DIR}/ ${DESTDIR}${SDKS_OUT_DIR}

sed -e 's,@bindir@,'$MONO_PREFIX'/bin,' -e 's,@mono_instdir@,'$MONO_PREFIX/lib/mono',' msbuild-mono-deploy.in > msbuild-mono-deploy.tmp
chmod +x msbuild-mono-deploy.tmp
cp msbuild-mono-deploy.tmp ${DESTDIR}${MONO_PREFIX}/bin/msbuild
rm -f msbuild-mono-deploy.tmp
