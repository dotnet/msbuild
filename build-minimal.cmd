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
REM   build-minimal.cmd -core            - Build only .NET Core (net10.0)
REM   build-minimal.cmd -netfx           - Build only .NET Framework (net472)
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
set "SingleTFM="

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
if /i "%~1"=="-core" (
    set "SingleTFM=net10.0"
    shift
    goto :parse_args
)
if /i "%~1"=="--core" (
    set "SingleTFM=net10.0"
    shift
    goto :parse_args
)
if /i "%~1"=="-netfx" (
    set "SingleTFM=net472"
    shift
    goto :parse_args
)
if /i "%~1"=="--netfx" (
    set "SingleTFM=net472"
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

REM If TFM filtering is requested, use dotnet build directly (faster)
if defined SingleTFM goto :tfm_build

REM Build arguments for full Arcade build
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
goto :build_done

:tfm_build
REM TFM-filtered build using MSBuild.exe (requires prior restore and Visual Studio)
echo.
echo ============================================================
echo  MSBuild Minimal Build - Single TFM
echo ============================================================
echo  Configuration:    %Configuration%
echo  Target Framework: %SingleTFM%
echo  Verbosity:        %Verbosity%
echo ============================================================
echo.

REM Check if restore has been done
if not exist "%RepoRoot%artifacts\obj\Microsoft.Build" (
    echo ERROR: No prior restore found. Please run one of these first:
    echo   - build-minimal.cmd         ^(builds and restores all TFMs^)
    echo   - build.cmd -restore        ^(restores only^)
    echo.
    echo Then run with -core or -netfx for fast single-TFM builds.
    exit /b 1
)

REM Find MSBuild.exe from Visual Studio
set "MSBuildExe="
for /f "delims=" %%i in ('where /r "C:\Program Files\Microsoft Visual Studio" MSBuild.exe 2^>nul ^| findstr /i "Current\\Bin\\MSBuild.exe"') do (
    if not defined MSBuildExe set "MSBuildExe=%%i"
)
if not defined MSBuildExe (
    echo ERROR: Visual Studio MSBuild.exe not found. -core and -netfx require Visual Studio.
    exit /b 1
)

REM Map verbosity to MSBuild format
set "MSBuildVerbosity=minimal"
if /i "%Verbosity%"=="q" set "MSBuildVerbosity=quiet"
if /i "%Verbosity%"=="quiet" set "MSBuildVerbosity=quiet"
if /i "%Verbosity%"=="m" set "MSBuildVerbosity=minimal"
if /i "%Verbosity%"=="n" set "MSBuildVerbosity=normal"
if /i "%Verbosity%"=="normal" set "MSBuildVerbosity=normal"
if /i "%Verbosity%"=="d" set "MSBuildVerbosity=detailed"
if /i "%Verbosity%"=="detailed" set "MSBuildVerbosity=detailed"

REM Build single TFM by building MSBuild.csproj directly with TargetFramework
REM This builds only the specified TFM and its dependencies (including netstandard2.0)
set "MSBuildArgs=%RepoRoot%src\MSBuild\MSBuild.csproj /p:Configuration=%Configuration%"
set "MSBuildArgs=%MSBuildArgs% /p:TargetFramework=%SingleTFM%"
set "MSBuildArgs=%MSBuildArgs% /restore:false /v:%MSBuildVerbosity%"
if defined Rebuild set "MSBuildArgs=%MSBuildArgs% /t:Rebuild"

"%MSBuildExe%" %MSBuildArgs% %ExtraArgs%
set "ExitCode=%ERRORLEVEL%"

REM Also build Bootstrap to create usable MSBuild
if %ExitCode%==0 (
    set "BootstrapArgs=%RepoRoot%src\MSBuild.Bootstrap\MSBuild.Bootstrap.csproj /p:Configuration=%Configuration%"
    set "BootstrapArgs=!BootstrapArgs! /p:TargetFramework=%SingleTFM%"
    set "BootstrapArgs=!BootstrapArgs! /restore:false /v:%MSBuildVerbosity%"
    if defined Rebuild set "BootstrapArgs=!BootstrapArgs! /t:Rebuild"
    
    "%MSBuildExe%" !BootstrapArgs! %ExtraArgs%
    set "ExitCode=!ERRORLEVEL!"
)

:build_done

if %ExitCode%==0 (
    echo.
    echo ============================================================
    echo  Build succeeded!
    if defined SingleTFM (
        echo.
        if "%SingleTFM%"=="net10.0" (
            echo  Bootstrap: artifacts\bin\bootstrap\core\dotnet.exe build
        ) else (
            echo  Bootstrap: artifacts\bin\bootstrap\net472\MSBuild\Current\Bin\MSBuild.exe
        )
    ) else if "%CreateBootstrap%"=="true" (
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
echo   -core           Build only .NET Core (net10.0) - requires prior restore
echo   -netfx          Build only .NET Framework (net472) - requires prior restore
echo   -release        Build in Release configuration (default: Debug)
echo   -debug          Build in Debug configuration
echo   -rebuild        Force a rebuild (clean + build)
echo   -v ^<level^>      Verbosity: q[uiet], m[inimal], n[ormal], d[etailed]
echo   -help           Show this help
echo.
echo Examples:
echo   build-minimal.cmd                     Minimal build with bootstrap
echo   build-minimal.cmd -nobootstrap        Fast incremental build (no bootstrap)
echo   build-minimal.cmd -core -nobootstrap  Fastest: single TFM, no bootstrap
echo   build-minimal.cmd -netfx              Build only .NET Framework
echo   build-minimal.cmd -release            Release build
echo.
echo Note: -core and -netfx require a prior restore (run build-minimal.cmd once first)
echo.
echo For full builds including tests, use: build.cmd
echo.
exit /b 0
