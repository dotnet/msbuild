#!/bin/bash

export TestSubjectMSBuildSDKsPath=$HELIX_CORRELATION_PAYLOAD/s
export DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=$TestSubjectMSBuildSDKsPath
export MicrosoftNETBuildExtensionsTargets=$TestSubjectMSBuildSDKsPath/Microsoft.NET.Build.Extensions/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
export DOTNET_ROOT=$HELIX_CORRELATION_PAYLOAD/d
export PATH=$DOTNET_ROOT:$PATH

export TestExecutionDirectory=$(pwd)/testExecutionDirectory
mkdir $TestExecutionDirectory
cp -a $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/. $TestExecutionDirectory/
