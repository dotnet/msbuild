#!/bin/sh
if [ $# -ne 1 ]; then
	echo "Usage: install-mono.sh </path/to/mono/installation>"
	exit 1
fi

MSBUILD_TOOLSVERSION=15.0
MONO_PREFIX=$1
MSBUILD_OUT_DIR="bin/Debug-MONO/*_Deployment"
MSBUILD_INSTALL_BIN_DIR="$MONO_PREFIX/lib/mono/msbuild/${MSBUILD_TOOLSVERSION}/bin"
XBUILD_DIR=$MONO_PREFIX/lib/mono/xbuild

mkdir -p ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}
mkdir -p ${DESTDIR}${XBUILD_DIR}/$MSBUILD_TOOLSVERSION
mkdir -p ${DESTDIR}${MONO_PREFIX}/bin

cp -r $MSBUILD_OUT_DIR/* ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}
mv ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/Microsoft.Common.props ${DESTDIR}${XBUILD_DIR}/$MSBUILD_TOOLSVERSION

rm ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/*UnitTests*
rm ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/*xunit*
rm ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/NuGet*
rm ${DESTDIR}${MSBUILD_INSTALL_BIN_DIR}/System.Runtime.InteropServices.RuntimeInformation.dll

cp -R nuget-support/tv/* ${DESTDIR}${XBUILD_DIR}/$MSBUILD_TOOLSVERSION
cp -R nuget-support/tasks-targets/* ${DESTDIR}${XBUILD_DIR}/

for f in ${XBUILD_DIR}/Microsoft/NuGet/*; do ln -s $f ${DESTDIR}${XBUILD_DIR} ; done

sed -e 's,@bindir@,'$MONO_PREFIX'/bin,' -e 's,@mono_instdir@,'$MONO_PREFIX/lib/mono',' msbuild-mono-deploy.in > msbuild-mono-deploy.tmp
chmod +x msbuild-mono-deploy.tmp
cp msbuild-mono-deploy.tmp ${DESTDIR}${MONO_PREFIX}/bin/msbuild
rm -f msbuild-mono-deploy.tmp
