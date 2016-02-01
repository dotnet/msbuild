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

if not defined MSBUILDLOGPATH (
    set MSBUILDLOGPATH=%~dp0msbuild.log
)

echo ** MSBuild Path: %MSBUILDCUSTOMPATH%
echo ** Building all sources

:: Call MSBuild
echo ** "%MSBUILDCUSTOMPATH%" "%~dp0build.proj" /verbosity:minimal /nodeReuse:false /fileloggerparameters:Verbosity=diag;LogFile="%MSBUILDLOGPATH%" %*
"%MSBUILDCUSTOMPATH%" "%~dp0build.proj" /verbosity:minimal /nodeReuse:false /fileloggerparameters:Verbosity=diag;LogFile="%MSBUILDLOGPATH%" %*
set BUILDERRORLEVEL=%ERRORLEVEL%
echo.

:: Pull the build summary from the log file
findstr /ir /c:".*Warning(s)" /c:".*Error(s)" /c:"Time Elapsed.*" "%MSBUILDLOGPATH%"
echo ** Build completed. Log: %MSBUILDLOGPATH% Exit code: %BUILDERRORLEVEL%

exit /b %BUILDERRORLEVEL%
