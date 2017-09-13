@echo off

REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

REM Get normalized version of parent path
for %%i in (%~dp0..\) DO (
    SET SDK_REPO_ROOT=%%~dpi
)

title SDK Build (%SDK_REPO_ROOT%)
set PATH=%SDK_REPO_ROOT%.dotnet_cli;%PATH%
set /P SDK_CLI_VERSION=<%SDK_REPO_ROOT%DotnetCLIVersion.txt
rem set DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=%SDK_REPO_ROOT%.dotnet_cli\sdk\%SDK_CLI_VERSION%\Sdks
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_MULTILEVEL_LOOKUP=0

set NUGET_PACKAGES=%SDK_REPO_ROOT%packages