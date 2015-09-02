@echo off
setlocal

:: Check prerequisites
if not "%VisualStudioVersion%" == "14.0" (
    echo Error: build.cmd should be run from a Visual Studio 2015 Command Prompt.  
    echo        Please see https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging for build instructions.
    exit /b 1
)

:: Check for a custom MSBuild path. If not defined, default to the one in your path.
if not defined MSBUILDCUSTOMPATH (
    set MSBUILDCUSTOMPATH=MSBuild.exe
)

set NUGETEXEPATH="%~dp0packages\NuGet.exe"

if not exist "%~dp0packages" mkdir "%~dp0packages"
if not exist "%NUGETEXEPATH%" (
    :: This will need to be fixed for non-windows
    echo ** Downloading NuGet.exe from https://nuget.org/NuGet.exe...
    echo PS^> Invoke-WebRequest -OutFile %NUGETEXEPATH% "https://nuget.org/NuGet.exe"
    powershell -Command "Invoke-WebRequest -OutFile %NUGETEXEPATH% "https://nuget.org/NuGet.exe""
)

echo Restoring NuGet packages
"%NUGETEXEPATH%" install "%~dp0src\.nuget\packages.config" -o "%~dp0packages"

echo ** MSBuild Path: %MSBUILDCUSTOMPATH%
echo ** Building all sources

:: Call MSBuild
echo ** "%MSBUILDCUSTOMPATH%" "%~dp0build.proj" /maxcpucount /verbosity:minimal /nodeReuse:false /fileloggerparameters:Verbosity=diag;LogFile="%~dp0msbuild.log" /t:BuildAndTest %*
"%MSBUILDCUSTOMPATH%" "%~dp0build.proj" /maxcpucount /verbosity:minimal /nodeReuse:false /fileloggerparameters:Verbosity=diag;LogFile="%~dp0msbuild.log" /t:BuildAndTest %*
set BUILDERRORLEVEL=%ERRORLEVEL%
echo.

:: Pull the build summary from the log file
findstr /ir /c:".*Warning(s)" /c:".*Error(s)" /c:"Time Elapsed.*" "%~dp0msbuild.log"
echo ** Build completed. Exit code: %BUILDERRORLEVEL%

exit /b %BUILDERRORLEVEL%
