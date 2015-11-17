@echo off

REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

CALL %~dp0..\build.cmd %*

exit /b %errorlevel%
