:: Usage:
:: BuildAndCopy <path> <retail framework>
::    <path> - Where to copy the build output
::    <retail framework> - true to have MSBuild target Microsoft.Build.Framework
:: 
:: Example: BuildAndCopy.cmd bin\MSBuild false

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
    if "%2"=="true" (
        set AdditionalBuildCommand= /p:TargetRetailBuildFramework=true
    )
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

:: This is a bit hacky, but we need to copy certain dependencies.
:: The files needed are defined in MSBuildLocalSystemDependencies.txt (one per line)
:: Note: Files may be in use if the compiler is still running (this is generally ok)
echo ** Copying required dependencies from MSBuild 14.0
for /F "tokens=*" %%A in (MSBuildLocalSystemDependencies.txt) do (
	robocopy "%MSBuild14Path%" "%OutputPath%" %%A /NFL /NDL /NJH /NJS /nc /ns /np>nul
)
echo.

:: Delete the copy of Microsoft.Build.Framework.dll we built so there are no conflicts
if "%2"=="true" (
    echo ** Deleting Microsoft.Build.Framework.dll we built in favor of the Retail version.
	del %OutputPath%\Microsoft.Build.Framework.*
)

echo.
echo ** Packaging complete.
set MSBUILDCUSTOMPATH="%OutputPath%\MSBuild.exe"
echo ** MSBuild = %MSBUILDCUSTOMPATH%