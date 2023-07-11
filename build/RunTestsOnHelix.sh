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

export DOTNET_SDK_TEST_EXECUTION_DIRECTORY=$TestExecutionDirectory
export DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=$HELIX_CORRELATION_PAYLOAD/r
export DOTNET_SDK_TEST_ASSETS_DIRECTORY=$TestExecutionDirectory/Assets

# call dotnet new so the first run message doesn't interfere with the first test
dotnet new --debug:ephemeral-hive

# We downloaded a special zip of files to the .nuget folder so add that as a source
dotnet nuget list source --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget add source $DOTNET_ROOT/.nuget --configfile $TestExecutionDirectory/NuGet.config
#Remove feeds not needed for tests
dotnet nuget remove source dotnet6-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet6-internal-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet7-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet7-internal-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source richnav --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source vs-impl --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-libraries-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-tools-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-libraries --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-eng --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget list source --configfile $TestExecutionDirectory/NuGet.config

cp $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/testAsset.props ./
export TestPackagesRoot=$(pwd)/Assets/TestPackages
dotnet build ./Assets/TestPackages/Microsoft.NET.TestPackages.csproj /t:Build -p:VersionPropsIsImported=false
mkdir $TestExecutionDirectory/Testpackages
cp -v $TestPackagesRoot/TestPackages/* $TestExecutionDirectory/Testpackages/.
dotnet nuget add source $TestExecutionDirectory/Testpackages --configfile $TestExecutionDirectory/NuGet.config

