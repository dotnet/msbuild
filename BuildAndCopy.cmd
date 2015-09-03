:: Usage:
:: BuildAndCopy <path> <retail framework>
::    <path> - Where to copy the build output
:: 
:: Example: BuildAndCopy.cmd bin\MSBuild

@echo off
setlocal

set MSBuild14Path=%ProgramFiles(x86)%\MSBuild\14.0\Bin
set DebugBuildOutputPath=%~dp0bin\Windows_NT\Debug
set OutputPath=%~dp0bin\MSBuild

:: Check prerequisites
if not defined VS140COMNTOOLS (
    echo Error: This script should be run from a Visual Studio 2015 Command Prompt.  
    echo        Please see https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging for build instructions.
    exit /b 1
)

if not "%1"=="" (
    set OutputPath=%1
)

echo ** Creating a build package
echo ** Output Path: %OutputPath%
echo ** Additional Build Parameters:%AdditionalBuildCommand%
echo.
:: Build MSBuild
call "%~dp0build.cmd" /t:Rebuild %AdditionalBuildCommand%

:: Make a copy of our build
echo ** ROBOCOPY bin\Windows_NT\Debug -^> %OutputPath%
robocopy "%DebugBuildOutputPath%" "%OutputPath%" *.* /S /NFL /NDL /NJH /NJS /nc /ns /np
echo.

echo.
echo ** Packaging complete.
set MSBUILDCUSTOMPATH="%OutputPath%\MSBuild.exe"
echo ** MSBuild = %MSBUILDCUSTOMPATH%
