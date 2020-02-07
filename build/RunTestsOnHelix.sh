#!/bin/bash

export TestSubjectMSBuildSDKsPath=$HELIX_CORRELATION_PAYLOAD/s
export DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=$TestSubjectMSBuildSDKsPath
export MicrosoftNETBuildExtensionsTargets=$HELIX_CORRELATION_PAYLOAD/ex/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
export DOTNET_ROOT=$HELIX_CORRELATION_PAYLOAD/d
export PATH=$DOTNET_ROOT:$PATH

export TestExecutionDirectory=$(pwd)/testExecutionDirectory
mkdir $TestExecutionDirectory
export DOTNET_CLI_HOME=$TestExecutionDirectory/.dotnet
cp -a $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/. $TestExecutionDirectory/
