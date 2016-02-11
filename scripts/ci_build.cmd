@echo off

REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

set CI_BUILD=1
set SKIP_CROSSGEN=0

CALL %~dp0..\build.cmd %*

exit /b %errorlevel%
