@echo off
setlocal

set MSBUILD_ARGS="%~dp0build.proj" /m /verbosity:minimal /fileloggerparameters:Verbosity=diag;LogFile="%~dp0msbuild.log" %*

:: Check for a custom MSBuild path. If not defined, default to the one in your path.
if not defined MSBUILD_CUSTOM_PATH (
    set MSBUILD_CUSTOM_PATH=MSBuild.exe
)

:: Check for a runtime host. If not defined, do not use a host
if not defined RUNTIME_HOST (
	set BUILD_COMMAND="%MSBUILD_CUSTOM_PATH%" %MSBUILD_ARGS%

    :: Check prerequisites for full framework build
 	if not "%VisualStudioVersion%" == "14.0" (
	    echo Error: build.cmd should be run from a Visual Studio 2015 Command Prompt.  
	    echo        Please see https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging for build instructions.
	    exit /b 1
	)
) ELSE (
	set BUILD_COMMAND= "%RUNTIME_HOST%" "%MSBUILD_CUSTOM_PATH%" %MSBUILD_ARGS%
)

echo ** MSBuild Path: %MSBUILD_CUSTOM_PATH%
echo ** Runtime Host Path: %RUNTIME_HOST%
echo ** Building all sources

:: Restore build tools
call %~dp0init-tools.cmd

:: Call MSBuild
echo ** %BUILD_COMMAND%
call %BUILD_COMMAND%
set BUILDERRORLEVEL=%ERRORLEVEL%
echo.

:: Pull the build summary from the log file
findstr /ir /c:".*Warning(s)" /c:".*Error(s)" /c:"Time Elapsed.*" "%~dp0msbuild.log"
echo ** Build completed. Exit code: %BUILDERRORLEVEL%

exit /b %BUILDERRORLEVEL%
