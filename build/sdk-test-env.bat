@echo off

REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

REM Get normalized version of parent path
for %%i in (%~dp0..\) DO (
    SET SDK_REPO_ROOT=%%~dpi
)

title SDK Test (%SDK_REPO_ROOT%)
set DOTNET_MULTILEVEL_LOOKUP=0
set PATH=%SDK_REPO_ROOT%.dotnet_cli;%PATH%
set NUGET_PACKAGES=%SDK_REPO_ROOT%packages
set /P SDK_CLI_VERSION=<%SDK_REPO_ROOT%DotnetCLIVersion.txt
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set MSBuildSDKsPath=%SDK_REPO_ROOT%bin\Debug\Sdks
set DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=%SDK_REPO_ROOT%bin\Debug\Sdks
set NETCoreSdkBundledVersionsProps=%SDK_REPO_ROOT%.dotnet_cli\sdk\%SDK_CLI_VERSION%\Microsoft.NETCoreSdk.BundledVersions.props
set CustomAfterMicrosoftCommonTargets=%SDK_REPO_ROOT%bin\Debug\Sdks\Microsoft.NET.Build.Extensions\msbuildExtensions-ver\Microsoft.Common.Targets\ImportAfter\Microsoft.NET.Build.Extensions.targets
set MicrosoftNETBuildExtensionsTargets=%SDK_REPO_ROOT%bin\Debug\Sdks\Microsoft.NET.Build.Extensions\msbuildExtensions\Microsoft\Microsoft.NET.Build.Extensions\Microsoft.NET.Build.Extensions.targets
rem You also need to add https://dotnet.myget.org/F/dotnet-core/api/v3/index.json to your NuGet feeds if building projects outside the SDK cone