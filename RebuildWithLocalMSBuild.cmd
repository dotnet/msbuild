:: This script will:
::   1) Rebuild MSBuild source tree.
::   2) Create a copy of the build output in bin\MSBuild
::   3) Build the source tree again with the MSBuild.exe in step 2.

@echo off
setlocal

set MSBUILD_CUSTOM_PATH=%~dp0Tools\MSBuild.exe
set RUNTIME_HOST=%~dp0Tools\CoreRun.exe

:: Build and run tests on CoreCLR
"%~dp0build.cmd" /t:RebuildAndTest /p:Configuration=Debug-NetCore
