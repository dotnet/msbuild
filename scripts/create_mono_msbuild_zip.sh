#!/bin/sh
COMMIT_ID=`git rev-parse --short HEAD`
ZIP_PATH=${PWD}/mono_msbuild_${COMMIT_ID}.zip

TMP_DIR=`mktemp -d`
MSBUILD_OUT_DIR=${TMP_DIR}/msbuild

cp -r bin/Debug-MONO/OSX_Deployment/ $MSBUILD_OUT_DIR
rm $MSBUILD_OUT_DIR/*UnitTests*
rm $MSBUILD_OUT_DIR/*xunit*

cp mono_msbuild_deploy.in $MSBUILD_OUT_DIR/msbuild
(cd $TMP_DIR; zip -r ${ZIP_PATH} msbuild)

rm -Rf $TMP_DIR

echo "Generated ${ZIP_PATH}"
