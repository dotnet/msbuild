:: Usage:
:: BuildAndCopy
:: 
:: Example: BuildAndCopy.cmd

@echo off
setlocal

set DebugBuildOutputPath=%~dp0bin\Windows_NT\Debug

:: Check prerequisites
if not defined VS140COMNTOOLS (
    echo Error: This script should be run from a Visual Studio 2015 Command Prompt.  
    echo        Please see https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging for build instructions.
    exit /b 1
)

if not "%1"=="" (
    set OutputPath=%1
)

echo ** Building with the installed MSBuild
echo ** Output Path: %DebugBuildOutputPath%
echo ** Additional Build Parameters:%AdditionalBuildCommand%
echo.
:: Build MSBuild
call "%~dp0build.cmd" /t:Rebuild %AdditionalBuildCommand%
set BUILDERRORLEVEL=%ERRORLEVEL%

:: Kill Roslyn, which may have handles open to files we want
taskkill /F /IM vbcscompiler.exe

if %BUILDERRORLEVEL% NEQ 0 (
    echo.
    echo Failed to build with errorlevel %BUILDERRORLEVEL% 1>&2
    exit /b %BUILDERRORLEVEL%
)

:: Make a copy of our build
echo ** Copying bootstrapped MSBuild to the bootstrap folder
msbuild /verbosity:minimal CreatePrivateMSBuildEnvironment.proj
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Failed building CreatePrivateMSBuildEnvironment.proj with error %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo ** Packaging complete.
echo ** MSBuild = %~dp0\bin\bootstrap\14.1\MSBuild.exe
