@echo off

REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

set SRC=%1

set SRC=%SRC:/=\%

pushd %~dp0..\..
set DST=%CD%\artifacts\win7-x64\stage2\bin
popd

if not exist "%SRC%" goto end
if not exist "%DST%" goto skip

xcopy /F /Y /I "%SRC%" "%DST%"

goto end

:skip
echo The destination "%DST%" does not exist. This script is only designed to update a previous full build!

:end
