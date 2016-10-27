@if not defined _echo echo off
setlocal ENABLEDELAYEDEXPANSION ENABLEEXTENSIONS
SET _originalScript=%~f0

:parseArguments
if "%1"=="" goto doneParsingArguments
if /i "%1"=="--scope" set SCOPE=%2&& shift && shift && goto parseArguments
if /i "%1"=="--target" set TARGET=%2&& shift && shift && goto parseArguments
if /i "%1"=="--host" set HOST=%2&& shift && shift && goto parseArguments
if /i "%1"=="--build-only" set BUILD_ONLY=true&& shift && goto parseArguments
if /i "%1"=="--bootstrap-only" set BOOTSTRAP_ONLY=true&& shift && goto parseArguments
if /i "%1"=="--localized-build" set LOCALIZED_BUILD=true&& shift && goto parseArguments
if /i "%1"=="--sync-xlf" set SYNC_XLF=true&& shift && goto parseArguments

:: Unknown parameters
goto :usage

:doneParsingArguments

:: Default target
set TARGET_ARG=RebuildAndTest

if /i "%SCOPE%"=="Compile" set TARGET_ARG=Build
if /i "%SCOPE%"=="Build" set TARGET_ARG=Build

:: Assign target configuration

:: Default to full-framework build
if not defined TARGET (
    set TARGET=Desktop
)

set BUILD_CONFIGURATION=
if /i "%TARGET%"=="CoreCLR" (
    set BUILD_CONFIGURATION=Debug-NetCore
) else if /i "%TARGET%"=="Desktop" (
    set BUILD_CONFIGURATION=Debug
) else if /i "%TARGET%"=="All" (
    SET _originalArguments=%*
    CALL "!_originalScript!" !_originalArguments:All=Desktop!
    IF ERRORLEVEL 1 GOTO :error
    CALL "!_originalScript!" !_originalArguments:All=CoreCLR!
    IF ERRORLEVEL 1 GOTO :error
    EXIT /B 0
) else (
    echo Unsupported target detected: %TARGET%. Aborting.
    goto :error
)

:: Assign runtime host

:: By default match host to target
if not defined HOST (
    if /i "%TARGET%"=="CoreCLR" (
        set HOST=CoreCLR
    ) else (
        set HOST=Desktop
    )
)

set RUNTIME_HOST=
if /i "%HOST%"=="CoreCLR" (
    set RUNTIME_HOST=%~dp0Tools\DotNetCLI\Dotnet.exe
    set MSBUILD_CUSTOM_PATH=%~dp0Tools\MSBuild.exe
) else if /i "%HOST%"=="Desktop" (
    set RUNTIME_HOST=
) else (
    echo Unsupported host detected: %HOST%. Aborting.
    goto :error
)

set LOCALIZED_BUILD_ARGUMENT=
if "%LOCALIZED_BUILD%"=="true" (
    set LOCALIZED_BUILD_ARGUMENT="/p:LocalizedBuild=true"
)

set SYNC_XLF_ARGUMENT=
if "%SYNC_XLF%"=="true" (
    set SYNC_XLF_ARGUMENT="/p:SyncXlf=true"
)

:: Restore build tools
call %~dp0init-tools.cmd

echo.
echo ** Rebuilding MSBuild with downloaded binaries

set MSBUILDLOGPATH=%~dp0msbuild_bootstrap_build.log
call "%~dp0build.cmd" /t:Rebuild /p:Configuration=%BUILD_CONFIGURATION% /p:"SkipBuildPackages=true" %LOCALIZED_BUILD_ARGUMENT% %SYNC_XLF_ARGUMENT%

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Bootstrap build failed with errorlevel %ERRORLEVEL% 1>&2
    goto :error
)
if "%BUILD_ONLY%"=="true" goto :success

:: Move initial build to bootstrap directory

echo.
echo ** Moving bootstrapped MSBuild to the bootstrap folder

:: Kill Roslyn, which may have handles open to files we want
taskkill /F /IM vbcscompiler.exe

set MSBUILDLOGPATH=%~dp0msbuild_move_bootstrap.log
set MSBUILD_ARGS=/verbosity:minimal BootStrapMSbuild.proj /p:Configuration=%BUILD_CONFIGURATION%

call "%~dp0build.cmd"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Failed to create bootstrap folder with errorlevel %ERRORLEVEL% 1>&2
    goto :error
)
if "%BOOTSTRAP_ONLY%"=="true" goto :success

set MSBUILD_ARGS=

:: Rebuild with bootstrapped msbuild
set MSBUILDLOGPATH=%~dp0msbuild_local_build.log

:: Only CoreCLR requires an override--it should use the host
:: downloaded as part of its NuGet package references, rather
:: than the possibly-stale one from Tools.
if /i "%TARGET%"=="CoreCLR" (
    set RUNTIME_HOST=%~dp0Tools\DotNetCLI\Dotnet.exe
)

if /i "%TARGET%"=="CoreCLR" (
    set MSBUILD_CUSTOM_PATH="%~dp0bin\Bootstrap\MSBuild.dll"
) else (
    set MSBUILD_CUSTOM_PATH="%~dp0bin\Bootstrap\15.0\Bin\MSBuild.exe"
)

echo.
echo ** Rebuilding MSBuild with locally built binaries

call "%~dp0build.cmd" /t:%TARGET_ARG% /p:Configuration=%BUILD_CONFIGURATION% %LOCALIZED_BUILD_ARGUMENT%

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

:usage
echo Options
echo   --scope ^<scope^>                Scope of the build ^(Compile / Test^)
echo   --target ^<target^>              CoreCLR, Desktop, or All ^(default: Desktop^)
echo   --host ^<host^>                  CoreCLR or Desktop ^(default: Desktop^)
echo   --build-only                     Only build using a downloaded copy of MSBuild but do not bootstrap
echo                                    or build again with those binaries
echo   --bootstrap-only                 Build and bootstrap MSBuild but do not build again with those binaries
echo   --localized-build                Do a localized build
echo   --sync-xlf                       Synchronize xlf files from resx files
exit /b 1

:error
echo.
echo ---------------------------------------
echo - cibuild.cmd FAILED. -
echo ---------------------------------------
exit /b 1
