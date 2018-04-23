#!/bin/sh

if [ $# -ne 2 ]; then
    echo "Usage: $0 <mono_prefix_dir> <out_dir>"
    exit 1
fi

REPO_ROOT="$PWD/../../"

sed -e 's,@bindir@,'$1'/bin,' -e 's,@mono_instdir@,'$1/lib/mono',' $REPO_ROOT/msbuild-mono-deploy.in > msbuild-mono-deploy.tmp
chmod +x msbuild-mono-deploy.tmp

mkdir -p $2
cp msbuild-mono-deploy.tmp $2/msbuild
rm -f msbuild-mono-deploy.tmp
