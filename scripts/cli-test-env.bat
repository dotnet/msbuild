@echo off
REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

REM Get normalized version of parent path
for %%i in (%~dp0..\) DO (
    SET CLI_REPO_ROOT=%%~dpi
)

title CLI Test (%CLI_REPO_ROOT%)

REM Add Stage 2 CLI to path
set PATH=%CLI_REPO_ROOT%bin\2\win-x64\dotnet;%PATH%

set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_MULTILEVEL_LOOKUP=0
set NUGET_PACKAGES=%CLI_REPO_ROOT%.nuget\packages
set TEST_PACKAGES=%CLI_REPO_ROOT%bin\2\win-x64\test\packages
set TEST_ARTIFACTS=%CLI_REPO_ROOT%bin\2\win-x64\test\artifacts
set PreviousStageProps=%CLI_REPO_ROOT%bin\2\win-x64\PreviousStage.props
