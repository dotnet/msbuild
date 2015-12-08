:: This script will:
::   1) Rebuild MSBuild source tree.
::   2) Create a copy of the build output in bin\MSBuild
::   3) Build the source tree again with the MSBuild.exe in step 2.

@echo off
setlocal

set MSBuildTempPath=%~dp0bin\MSBuild

:: Check prerequisites
if not defined VS140COMNTOOLS (
    echo Error: This script should be run from a Visual Studio 2015 Command Prompt.
    echo        Please see https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging for build instructions.
    exit /b 1
)

:: Build and copy output to bin\bootstrap
set MSBUILDLOGPATH=%~dp0msbuild_bootstrap.log
call "%~dp0BuildAndCopy.cmd"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo BuildAndCopy.cmd failed with errorlevel %ERRORLEVEL% 1>&2
    goto :error
)

:: Rebuild with bootstrapped msbuild
set MSBUILDLOGPATH=%~dp0msbuild_local.log
set MSBUILDCUSTOMPATH="%~dp0\bin\Bootstrap\14.1\Bin\MSBuild.exe"
"%~dp0build.cmd" /t:RebuildAndTest /p:BootstrappedMSBuild=true
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo build.cmd with bootstrapped MSBuild failed with errorlevel %ERRORLEVEL% 1>&2
    goto :error
)

echo.
echo ++++++++++++++++
echo + SUCCESS  :-) +
echo ++++++++++++++++
echo.
exit /b 0

:error
echo.
echo ---------------------------------------
echo - RebuildWithLocalMSBuild.cmd FAILED. -
echo ---------------------------------------
exit /b 1
