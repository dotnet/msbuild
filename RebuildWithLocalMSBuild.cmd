:: This script will:
::   1) Rebuild MSBuild source tree (x86 only).
::   2) Create a "bootstrapped" copy of the build output in bin\MSBuild.
::   3) Build the source tree again with the MSBuild.exe in step 2.

@echo off
setlocal

:: Restore build tools
call %~dp0init-tools.cmd

set RUNTIME_HOST=%~dp0Tools\dotnetcli\dotnet.exe

:: build MSBuild with the MSBuild binaries from BuildTools
set MSBUILDLOGPATH=%~dp0msbuild_bootstrap_build.log
set MSBUILD_CUSTOM_PATH=%~dp0Tools\MSBuild.exe

echo.
echo ** Rebuilding MSBuild with binaries from BuildTools

call "%~dp0build.cmd" /t:Rebuild /p:Configuration=Debug-NetCore /p:"OverrideToolHost=%RUNTIME_HOST%"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Bootstrap build failed with errorlevel %ERRORLEVEL% 1>&2
    goto :error
)

:: Move initial build to bootstrap directory

:: Kill Roslyn, which may have handles open to files we want
taskkill /F /IM vbcscompiler.exe

set MSBUILDLOGPATH=%~dp0msbuild_move_bootstrap.log
set MSBUILD_ARGS=/verbosity:minimal targets\BootStrapMSbuild.proj /p:Configuration=Debug-NetCore

echo.
echo ** Moving bootstrapped MSBuild to the bootstrap folder

call "%~dp0build.cmd"
set MSBUILD_ARGS=

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo build.cmd with bootstrapped MSBuild failed with errorlevel %ERRORLEVEL% 1>&2
    goto :error
)

:: Rebuild with bootstrapped msbuild
set MSBUILDLOGPATH=%~dp0msbuild_local_build.log
set RUNTIME_HOST="%~dp0bin\Bootstrap\CoreRun.exe"
set MSBUILD_CUSTOM_PATH="%~dp0bin\Bootstrap\MSBuild.exe"

echo.
echo ** Rebuilding MSBuild with locally built binaries

call "%~dp0build.cmd" /t:RebuildAndTest /p:Configuration=Debug-NetCore

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Local build failed with error level %ERRORLEVEL% 1>&2
    goto :error
)

:success
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
