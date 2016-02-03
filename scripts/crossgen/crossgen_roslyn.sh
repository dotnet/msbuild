#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done

DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/../common/_common.sh"

if $SKIP_CROSSGEN ; then
    echo "Skipping Crossgen"
    exit 0
fi


info "Crossgenning Roslyn compiler ..."

set -e

BIN_DIR="$( cd $1 && pwd )"

UNAME=`uname`

# Always recalculate the RID because the package always uses a specific RID, regardless of OS X version or Linux distro.
if [ "$OSNAME" == "osx" ]; then
    RID=osx.10.10-x64
elif [ "$OSNAME" == "ubuntu" ]; then
    RID=ubuntu.14.04-x64
elif [ "$OSNAME" == "centos" ]; then
    RID=rhel.7-x64
else
    echo "Unknown OS: $OSNAME" 1>&2
    exit 1
fi

READYTORUN=""

# Replace with a robust method for finding the right crossgen.exe
CROSSGEN_UTIL=$NUGET_PACKAGES/runtime.$RID.Microsoft.NETCore.Runtime.CoreCLR/1.0.1-rc2-23811/tools/crossgen

cd $BIN_DIR

# Crossgen currently requires itself to be next to mscorlib
cp $CROSSGEN_UTIL $BIN_DIR
chmod +x crossgen

./crossgen -nologo $READYTORUN -platform_assemblies_paths $BIN_DIR mscorlib.dll

info "Crossgenning System.Collections.Immutable"
./crossgen -nologo $READYTORUN -platform_assemblies_paths $BIN_DIR System.Collections.Immutable.dll

info "Crossgenning System.Reflection.Metadata"
./crossgen -nologo $READYTORUN -platform_assemblies_paths $BIN_DIR System.Reflection.Metadata.dll

info "Crossgenning Microsoft.CodeAnalysis"
./crossgen -nologo $READYTORUN -platform_assemblies_paths $BIN_DIR Microsoft.CodeAnalysis.dll

info "Crossgenning Microsoft.CodeAnalysis.CSharp"
./crossgen -nologo $READYTORUN -platform_assemblies_paths $BIN_DIR Microsoft.CodeAnalysis.CSharp.dll

info "Crossgenning Microsoft.CodeAnalysis.CSharp.Scripting"
./crossgen -nologo $READYTORUN -platform_assemblies_paths $BIN_DIR Microsoft.CodeAnalysis.CSharp.Scripting.dll

info "Crossgenning Microsoft.CodeAnalysis.VisualBasic"
./crossgen -nologo $READYTORUN -platform_assemblies_paths $BIN_DIR Microsoft.CodeAnalysis.VisualBasic.dll

info "Crossgenning csc"
./crossgen -nologo $READYTORUN -platform_assemblies_paths $BIN_DIR csc.dll
[ -e csc.ni.exe ] && [ ! -e csc.ni.dll ] && mv csc.ni.exe csc.ni.dll

info "Crossgenning csi"
./crossgen -nologo $READYTORUN -platform_assemblies_paths $BIN_DIR csi.dll
[ -e csi.ni.exe ] && [ ! -e csi.ni.dll ] && mv csi.ni.exe csi.ni.dll

info "Crossgenning vbc"
./crossgen -nologo $READYTORUN -platform_assemblies_paths $BIN_DIR vbc.dll
[ -e vbc.ni.exe ] && [ ! -e vbc.ni.dll ] && mv vbc.ni.exe vbc.ni.dll

info "CrossGen Roslyn Finished"
