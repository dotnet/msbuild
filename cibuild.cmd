@if not defined _echo echo off
setlocal ENABLEDELAYEDEXPANSION ENABLEEXTENSIONS
SET _originalScript=%~f0

:parseArguments
if "%1"=="" goto doneParsingArguments
if /i "%1"=="--scope" set SCOPE=%2&& shift && shift && goto parseArguments
if /i "%1"=="--target" set TARGET=%2&& shift && shift && goto parseArguments
if /i "%1"=="--host" set HOST=%2&& shift && shift && goto parseArguments
if /i "%1"=="--config" set BASE_CONFIG=%2&& shift && shift && goto parseArguments
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
    set TARGET=Full
)

if not defined BASE_CONFIG (
    set BASE_CONFIG=Debug
)

set BUILD_CONFIGURATION=
if /i "%TARGET%"=="CoreCLR" (
    set BUILD_CONFIGURATION=%BASE_CONFIG%-NetCore
) else if /i "%TARGET%"=="Full" (
    set BUILD_CONFIGURATION=%BASE_CONFIG%
) else if /i "%TARGET%"=="All" (
    SET _originalArguments=%*
    CALL "!_originalScript!" !_originalArguments:All=Full!
    IF ERRORLEVEL 1 GOTO :error
    CALL "!_originalScript!" !_originalArguments:All=CoreCLR!
    IF ERRORLEVEL 1 GOTO :error
    EXIT /B 0
) else (
    echo Unsupported target detected: %TARGET%. Configuring as if for Full.
    set TARGET=Full
    set BUILD_CONFIGURATION=%BASE_CONFIG%
)

echo Using Configuration: %BUILD_CONFIGURATION%

:: Assign runtime host

:: By default match host to target
if not defined HOST (
    if /i "%TARGET%"=="CoreCLR" (
        set HOST=CoreCLR
    ) else (
        set HOST=Full
    )
)

set RUNTIME_HOST=
if /i "%HOST%"=="CoreCLR" (
    set RUNTIME_HOST=%~dp0Tools\DotNetCLI\Dotnet.exe
    set MSBUILD_CUSTOM_PATH=%~dp0Tools\MSBuild.exe
) else if /i "%HOST%"=="Full" (
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

set MSBUILDLOGPATH=%~dp0msbuild_bootstrap_build-%HOST%.log
call "%~dp0build.cmd" /t:Rebuild /p:Configuration=%BUILD_CONFIGURATION% /p:"SkipBuildPackages=true" %LOCALIZED_BUILD_ARGUMENT% %SYNC_XLF_ARGUMENT% %RUNTIMETYPE_ARGUMENT%

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

set MSBUILDLOGPATH=%~dp0msbuild_move_bootstrap-%HOST%.log
set MSBUILD_ARGS=/verbosity:minimal targets\BootStrapMSbuild.proj /p:Configuration=%BUILD_CONFIGURATION% %RUNTIMETYPE_ARGUMENT%

call "%~dp0build.cmd"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Failed to create bootstrap folder with errorlevel %ERRORLEVEL% 1>&2
    goto :error
)
if "%BOOTSTRAP_ONLY%"=="true" goto :success

set MSBUILD_ARGS=

:: Rebuild with bootstrapped msbuild
set MSBUILDLOGPATH=%~dp0msbuild_local_build-%HOST%.log

:: Only CoreCLR requires an override--it should use the host
:: downloaded as part of its NuGet package references, rather
:: than the possibly-stale one from Tools.
if /i "%TARGET%"=="CoreCLR" (
    set RUNTIME_HOST=%~dp0Tools\DotNetCLI\Dotnet.exe
)

if /i "%TARGET%"=="CoreCLR" (
    set MSBUILD_CUSTOM_PATH="%~dp0bin\Bootstrap-NetCore\MSBuild.dll"
) else (
    set MSBUILD_CUSTOM_PATH="%~dp0bin\Bootstrap\MSBuild\15.0\Bin\MSBuild.exe"
)

:: The set of warnings to suppress for now
:: warning MSB3277: Found conflicts between different versions of the same dependent assembly that could not be resolved.
:: warning MSB3026: Could not copy "XXX" to "XXX". Beginning retry 1 in 1000ms.
:: warning MSB3073: Exec task failure (when set to be a warning) -- needed to keep from failing on dev desktops that don't have C++ tools
:: warning AL1053: The version '1.2.3.4-foo' specified for the 'product version' is not in the normal 'major.minor.build.revision' format
SET _NOWARN=MSB3277;MSB3026;MSB3073;AL1053
set MSBUILDBINLOGPATH=%~dp0msbuild_rebuild-%HOST%.binlog

echo.
echo ** Rebuilding MSBuild with locally built binaries

call "%~dp0build.cmd" /t:%TARGET_ARG% /p:Configuration=%BUILD_CONFIGURATION% %LOCALIZED_BUILD_ARGUMENT% "/nowarn:%_NOWARN%" /warnaserror /bl:%MSBUILDBINLOGPATH%

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Local build failed with error level %ERRORLEVEL% 1>&2
    goto :error
)

:: Only detect source control changes when running in the CI environment
:: Detect if there are any changed files which should fail the build
if DEFINED JENKINS_URL (
    echo Detecting changed files...
    git status
    git --no-pager diff HEAD --word-diff=plain --exit-code
    if ERRORLEVEL 1 (
        echo.
        echo [ERROR] After building, there are changed files.  Please build locally ^(cibuild.cmd --target All^) and include these changes in your pull request. 1>&2
        goto :error
    )
    goto :EOF
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
echo   --target ^<target^>              CoreCLR, Full, or All ^(default: Full^)
echo   --host ^<host^>                  CoreCLR or Full ^(default: Full^)
echo   --config ^<config^>              Debug or Release ^(default: Debug^)
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
