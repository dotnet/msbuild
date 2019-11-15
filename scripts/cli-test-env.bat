@echo off
REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

REM Get normalized version of parent path
for %%i in (%~dp0..\) DO (
    SET CLI_REPO_ROOT=%%~dpi
)

title CLI Test (%CLI_REPO_ROOT%)

REM Add Stage 2 CLI to path
set PATH=%CLI_REPO_ROOT%artifacts\bin\redist\Debug\dotnet;%PATH%

set DOTNET_MULTILEVEL_LOOKUP=0
set NUGET_PACKAGES=%CLI_REPO_ROOT%.nuget\packages
set PreviousStageProps=%CLI_REPO_ROOT%bin\2\PreviousStage.props

REM Prevent environment variable get into msbuild
set architecture=
