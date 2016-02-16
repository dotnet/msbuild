@echo off

REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

set CI_BUILD=1
set VERBOSE=1

REM Shift breaks %~dp0 so capture it first
set MY_DIR=%~dp0

REM Parse arguments
:loop
IF NOT "%1"=="" (
    IF "%1"=="-NoPackage" (
        SET DOTNET_BUILD_SKIP_PACKAGING=1
    ) ELSE IF "%CONFIGURATION%"=="" (
        SET CONFIGURATION=%1
    ) ELSE IF "%TARGETS%"=="" (
        SET TARGETS=%1
    ) ELSE (
        SET TARGETS=%TARGETS% %1
    )
    SHIFT
    GOTO :loop
)

CALL %MY_DIR%..\build.cmd %TARGETS%

exit /b %errorlevel%
