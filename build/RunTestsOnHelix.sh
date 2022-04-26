#!/usr/bin/env bash

# make NuGet network operations more robust
export NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true
export NUGET_EXPERIMENTAL_MAX_NETWORK_TRY_COUNT=6
export NUGET_EXPERIMENTAL_NETWORK_RETRY_DELAY_MILLISECONDS=1000

export MicrosoftNETBuildExtensionsTargets=$HELIX_CORRELATION_PAYLOAD/ex/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
export DOTNET_ROOT=$HELIX_CORRELATION_PAYLOAD/d
export PATH=$DOTNET_ROOT:$PATH

export TestExecutionDirectory=$(pwd)/testExecutionDirectory
mkdir $TestExecutionDirectory
export DOTNET_CLI_HOME=$TestExecutionDirectory/.dotnet
cp -a $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/. $TestExecutionDirectory/

# call dotnet new so the first run message doesn't interfere with the first test
dotnet new --debug:ephemeral-hive
# avoid potetial concurrency issues when nuget is creating nuget.config
dotnet nuget list source
