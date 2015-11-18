#!/usr/bin/env bash

# This file encapsulates the temporary steps to build the dotnet-compile-native command successfully
# The AppDepSDK package is a temporary artifact until we have CoreRT assemblies published to Nuget

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

source $SCRIPT_DIR/../_common.sh

REPO_ROOT="$SCRIPT_DIR/../.."
APPDEPS_PROJECT_DIR=$REPO_ROOT/src/Microsoft.DotNet.Tools.Compiler.Native/appdep

# Get Absolute Output Dir
pushd $1
OUTPUT_DIR="$(pwd)"
popd

## App Deps ##
pushd $APPDEPS_PROJECT_DIR
dotnet restore --packages $APPDEPS_PROJECT_DIR/packages
APPDEP_SDK=$APPDEPS_PROJECT_DIR/packages/toolchain*/*/
popd

mkdir -p $OUTPUT_DIR/appdepsdk
cp -a $APPDEP_SDK/. $OUTPUT_DIR/appdepsdk