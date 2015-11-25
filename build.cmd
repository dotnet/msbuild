@echo off

REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

powershell -NoProfile -NoLogo -Command "%~dp0scripts\build.ps1 %*; exit $LastExitCode;"
