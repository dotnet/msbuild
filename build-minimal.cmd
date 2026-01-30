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
REM   build-minimal.cmd -rebuild         - Force rebuild
REM   build-minimal.cmd -help            - Show help
REM ============================================================================

set "RepoRoot=%~dp0"
set "Configuration=Debug"
set "CreateBootstrap=true"
set "Rebuild="
set "Build=true"
set "Verbosity=minimal"
set "ExtraArgs="

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

REM Use solution filter for minimal projects only
set "BuildArgs=%BuildArgs% /p:Projects=%RepoRoot%MSBuild.Minimal.slnf"

REM Disable IBC optimization for minimal builds (requires VSSetup which we don't build)
set "BuildArgs=%BuildArgs% /p:UsingToolIbcOptimization=false /p:UsingToolVisualStudioIbcTraining=false"

echo.
echo ============================================================
echo  MSBuild Minimal Build
echo ============================================================
echo  Configuration:    %Configuration%
echo  Create Bootstrap: %CreateBootstrap%
echo  Verbosity:        %Verbosity%
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
) else (
    echo.
    echo Build failed with exit code %ExitCode%. Check errors above.
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
echo   -v ^<level^>      Verbosity: q[uiet], m[inimal], n[ormal], d[etailed]
echo   -help           Show this help
echo.
echo Examples:
echo   build-minimal.cmd                     Minimal build with bootstrap
echo   build-minimal.cmd -nobootstrap        Fast incremental build (no bootstrap)
echo   build-minimal.cmd -release            Release build
echo.
echo For full builds including tests, use: build.cmd
echo.
exit /b 0
