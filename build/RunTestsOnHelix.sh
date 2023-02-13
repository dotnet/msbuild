#!/bin/bash

export MicrosoftNETBuildExtensionsTargets=$HELIX_CORRELATION_PAYLOAD/ex/msbuildExtensions/Microsoft/Microsoft.NET.Build.Extensions/Microsoft.NET.Build.Extensions.targets
export DOTNET_ROOT=$HELIX_CORRELATION_PAYLOAD/d
export PATH=$DOTNET_ROOT:$PATH

export TestExecutionDirectory=$(pwd)/testExecutionDirectory
mkdir $TestExecutionDirectory
export DOTNET_CLI_HOME=$TestExecutionDirectory/.dotnet
cp -a $HELIX_CORRELATION_PAYLOAD/t/TestExecutionDirectoryFiles/. $TestExecutionDirectory/

# call dotnet new so the first run message doesn't interfere with the first test
dotnet new --debug:ephemeral-hive

dotnet nuget list source --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet6-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet6-internal-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source richnav --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source vs-impl --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-libraries-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-tools-transport --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-libraries --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget remove source dotnet-eng --configfile $TestExecutionDirectory/NuGet.config
dotnet nuget list source --configfile $TestExecutionDirectory/NuGet.config
