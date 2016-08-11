@echo off
setlocal

if not defined MSBUILDLOGPATH (
    set MSBUILDLOGPATH=%~dp0msbuild.log
)

:: Check for a custom MSBuild path. If not defined, default to the one in your path.
if not defined MSBUILD_CUSTOM_PATH (
    set MSBUILD_CUSTOM_PATH=MSBuild.exe
)

if not defined MSBUILD_ARGS (
    set MSBUILD_ARGS="%~dp0build.proj" /m /verbosity:minimal %*
)

:: Add a the file logger with diagnostic verbosity to the msbuild args
set MSBUILD_ARGS=%MSBUILD_ARGS% /fileloggerparameters:Verbosity=diag;LogFile="%MSBUILDLOGPATH%"

:: Check for a runtime host. If not defined, do not use a host
if not defined RUNTIME_HOST (
	set BUILD_COMMAND="%MSBUILD_CUSTOM_PATH%" /nodeReuse:false %MSBUILD_ARGS%

    :: Check prerequisites for full framework build
 	if not "%VisualStudioVersion%" == "14.0" (
	    echo Error: build.cmd should be run from a Visual Studio 2015 Command when RUNTIME_HOST is not defined. Prompt.  
	    echo        Please see https://github.com/Microsoft/msbuild/wiki/Building-Testing-and-Debugging for build instructions.
	    exit /b 1
	)
) ELSE (
	set BUILD_COMMAND= "%RUNTIME_HOST%" "%MSBUILD_CUSTOM_PATH%" %MSBUILD_ARGS% /p:"OverrideToolHost=%RUNTIME_HOST%"
)

echo.
echo ** Using the MSBuild in path: %MSBUILD_CUSTOM_PATH%
echo ** Using runtime host in path: %RUNTIME_HOST%

:: Call MSBuild
echo ** %BUILD_COMMAND%
call %BUILD_COMMAND%
set BUILDERRORLEVEL=%ERRORLEVEL%
echo.

:: Pull the build summary from the log file
findstr /ir /c:".*Warning(s)" /c:".*Error(s)" /c:"Time Elapsed.*" "%MSBUILDLOGPATH%"
echo.
echo ** Build completed. Exit code: %BUILDERRORLEVEL%

exit /b %BUILDERRORLEVEL%
