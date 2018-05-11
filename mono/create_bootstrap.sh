#!/bin/sh

# This creates a bootstrap from an exising mono installation
# This is just to ensure that we have the correct "matched" Roslyn

TMP_DIR=`mktemp -d`
MSBUILD_DST_DIR=${TMP_DIR}/msbuild

MONO_PREFIX_SRC_DIR=`dirname $(which mono)`
MONO_PREFIX_SRC_DIR="${MONO_PREFIX_SRC_DIR}/../"
MSBUILD_BIN_SRC_DIR=$MONO_PREFIX_SRC_DIR/lib/mono/msbuild/15.0/bin

ID=`msbuild -version | head -n 1 | awk -F\( '{print $3}' | cut -d \  -f 1 | sed -e 's,\/,-,'`
ZIP_PATH=${PWD}/mono_msbuild_${ID}.zip

echo "Building bootstrap from $MONO_PREFIX_SRC_DIR"

# -L so that we get the linked files in Roslyn/
cp -L -R $MSBUILD_BIN_SRC_DIR $MSBUILD_DST_DIR

# don't fallback to dotnet, everything is self contained here
rm -Rf $MSBUILD_DST_DIR/SdkResolvers/Microsoft.DotNet.MSBuildSdkResolver

mkdir $MSBUILD_DST_DIR/Extensions

cp -R $MONO_PREFIX_SRC_DIR/lib/mono/xbuild/ $MSBUILD_DST_DIR/Extensions
rm -Rf $MSBUILD_DST_DIR/Extensions/1[24].0

# adjust System.Reflection.Metadata.dll to be a link, so we use exactly
# the same as Roslyn
rm $MSBUILD_DST_DIR/System.Reflection.Metadata.dll
(cd $MSBUILD_DST_DIR; ln -s Roslyn/System.Reflection.Metadata.dll System.Reflection.Metadata.dll)

# wrapper script
cat > $MSBUILD_DST_DIR/msbuild << 'EOL'
#!/bin/bash
ABSOLUTE_PATH=$(cd `dirname "${BASH_SOURCE[0]}"` && pwd)/`basename "${BASH_SOURCE[0]}"`
THIS_DIR=`dirname $ABSOLUTE_PATH`
MONO_GC_PARAMS="nursery-size=64m,$MONO_GC_PARAMS" exec mono $MONO_OPTIONS $THIS_DIR/MSBuild.dll /p:MSBuildExtensionsPath=$THIS_DIR/Extensions "$@"
EOL

chmod +x $MSBUILD_DST_DIR/msbuild

(cd $TMP_DIR; zip -r ${ZIP_PATH} msbuild)
rm -Rf $TMP_DIR
