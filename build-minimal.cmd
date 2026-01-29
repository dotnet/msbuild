@echo off
setlocal EnableDelayedExpansion

REM ============================================================================
REM build-minimal.cmd - Fast build script for minimal MSBuild assemblies only
REM 
REM This script builds only the essential MSBuild runtime without tests, samples,
REM or package projects. It is significantly faster than a full build.
REM
REM Usage:
REM   build-minimal.cmd                  - Build with bootstrap (default)
REM   build-minimal.cmd -nobootstrap     - Build without bootstrap (fastest)
REM   build-minimal.cmd -release         - Build release configuration
REM   build-minimal.cmd -netcore         - Build only .NET Core (skip net472)
REM   build-minimal.cmd -netfx           - Build only .NET Framework (skip netcore)
REM   build-minimal.cmd -help            - Show help
REM ============================================================================

set "RepoRoot=%~dp0"
set "Configuration=Debug"
set "CreateBootstrap=true"
set "Rebuild="
set "Restore=true"
set "Build=true"
set "Verbosity=minimal"
set "ExtraArgs="
set "TargetFrameworkFilter="

:parse_args
if "%~1"=="" goto :end_parse
if /i "%~1"=="-help" goto :show_help
if /i "%~1"=="/help" goto :show_help
if /i "%~1"=="/?" goto :show_help
if /i "%~1"=="--help" goto :show_help
if /i "%~1"=="-nobootstrap" (
    set "CreateBootstrap=false"
    shift
    goto :parse_args
)
if /i "%~1"=="--nobootstrap" (
    set "CreateBootstrap=false"
    shift
    goto :parse_args
)
if /i "%~1"=="-release" (
    set "Configuration=Release"
    shift
    goto :parse_args
)
if /i "%~1"=="--release" (
    set "Configuration=Release"
    shift
    goto :parse_args
)
if /i "%~1"=="-debug" (
    set "Configuration=Debug"
    shift
    goto :parse_args
)
if /i "%~1"=="--debug" (
    set "Configuration=Debug"
    shift
    goto :parse_args
)
if /i "%~1"=="-rebuild" (
    set "Rebuild=-rebuild"
    shift
    goto :parse_args
)
if /i "%~1"=="--rebuild" (
    set "Rebuild=-rebuild"
    shift
    goto :parse_args
)
if /i "%~1"=="-restore" (
    set "Build="
    shift
    goto :parse_args
)
if /i "%~1"=="--restore" (
    set "Build="
    shift
    goto :parse_args
)
if /i "%~1"=="-netcore" (
    set "TargetFrameworkFilter=net10.0"
    shift
    goto :parse_args
)
if /i "%~1"=="--netcore" (
    set "TargetFrameworkFilter=net10.0"
    shift
    goto :parse_args
)
if /i "%~1"=="-core" (
    set "TargetFrameworkFilter=net10.0"
    shift
    goto :parse_args
)
if /i "%~1"=="--core" (
    set "TargetFrameworkFilter=net10.0"
    shift
    goto :parse_args
)
if /i "%~1"=="-netfx" (
    set "TargetFrameworkFilter=net472"
    shift
    goto :parse_args
)
if /i "%~1"=="--netfx" (
    set "TargetFrameworkFilter=net472"
    shift
    goto :parse_args
)
if /i "%~1"=="-v" (
    set "Verbosity=%~2"
    shift
    shift
    goto :parse_args
)
if /i "%~1"=="--verbosity" (
    set "Verbosity=%~2"
    shift
    shift
    goto :parse_args
)

REM Pass through any other arguments
set "ExtraArgs=%ExtraArgs% %~1"
shift
goto :parse_args

:end_parse

REM Build arguments
set "BuildArgs=-restore -configuration %Configuration% -v %Verbosity%"
if defined Build set "BuildArgs=%BuildArgs% -build"
if defined Rebuild set "BuildArgs=%BuildArgs% %Rebuild%"
set "BuildArgs=%BuildArgs% /p:CreateBootstrap=%CreateBootstrap%"

REM Target framework filter for faster builds
if defined TargetFrameworkFilter (
    set "BuildArgs=%BuildArgs% /p:TargetFrameworks=%TargetFrameworkFilter%"
)

REM Use solution filter for minimal projects only
set "BuildArgs=%BuildArgs% /p:Projects=%RepoRoot%MSBuild.Minimal.slnf"

echo.
echo ============================================================
echo  MSBuild Minimal Build
echo ============================================================
echo  Configuration:    %Configuration%
echo  Create Bootstrap: %CreateBootstrap%
echo  Verbosity:        %Verbosity%
if defined TargetFrameworkFilter echo  Target Framework: %TargetFrameworkFilter%
echo ============================================================
echo.

REM Run the build using PowerShell
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%RepoRoot%eng\common\build.ps1""" %BuildArgs% %ExtraArgs%"
set "ExitCode=%ERRORLEVEL%"

if %ExitCode%==0 (
    echo.
    echo ============================================================
    echo  Build succeeded!
    if "%CreateBootstrap%"=="true" (
        echo.
        echo  To use the bootstrapped MSBuild, run:
        echo    artifacts\msbuild-build-env.bat
        echo.
        echo  Then use 'dotnet build' with your locally-built MSBuild.
    )
    echo ============================================================
)

exit /b %ExitCode%

:show_help
echo.
echo MSBuild Minimal Build Script - Fast build for development
echo.
echo Usage: build-minimal.cmd [options]
echo.
echo Options:
echo   -nobootstrap    Skip creating the bootstrap folder (fastest builds)
echo   -release        Build in Release configuration (default: Debug)
echo   -debug          Build in Debug configuration
echo   -rebuild        Force a rebuild (clean + build)
echo   -restore        Restore only, don't build
echo   -netcore, -core Build only .NET Core target (net10.0)
echo   -netfx          Build only .NET Framework target (net472)
echo   -v ^<level^>      Verbosity: q[uiet], m[inimal], n[ormal], d[etailed]
echo   -help           Show this help
echo.
echo Examples:
echo   build-minimal.cmd                     Minimal build with bootstrap
echo   build-minimal.cmd -nobootstrap        Fast incremental build
echo   build-minimal.cmd -netcore            .NET Core only (faster)
echo   build-minimal.cmd -netfx              .NET Framework only
echo   build-minimal.cmd -release            Release build
echo.
echo For full builds including tests, use: build.cmd
echo.
exit /b 0
