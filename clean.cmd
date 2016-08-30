@echo off

REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

if "%~1"=="" (
  @call run.cmd clean -?
  @exit /b 1
) else (
  @call run.cmd clean %*
  @exit /b %ERRORLEVEL%
)
