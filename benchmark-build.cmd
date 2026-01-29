@echo off
setlocal EnableDelayedExpansion

REM ============================================================================
REM benchmark-build.cmd - Benchmark script for comparing build times
REM 
REM Runs various build scenarios and reports timing for each.
REM Use this to objectively measure build performance improvements.
REM ============================================================================

set "RepoRoot=%~dp0"
set "ResultsFile=%RepoRoot%build-benchmark-results.txt"

echo.
echo ============================================================================
echo  MSBuild Build Performance Benchmark
echo ============================================================================
echo.
echo  This script will run multiple build scenarios and measure their times.
echo  Results will be saved to: %ResultsFile%
echo.
echo  Press Ctrl+C to cancel, or any key to continue...
pause > nul
echo.

REM Initialize results file
echo MSBuild Build Performance Benchmark > "%ResultsFile%"
echo ================================== >> "%ResultsFile%"
echo. >> "%ResultsFile%"
echo Date: %date% %time% >> "%ResultsFile%"
echo Machine: %COMPUTERNAME% >> "%ResultsFile%"
echo. >> "%ResultsFile%"

REM Clean first
echo [1/8] Cleaning build artifacts...
call "%RepoRoot%build.cmd" -clean > nul 2>&1

REM ============================================================================
REM Benchmark 1: Full build (cold)
REM ============================================================================
echo [2/8] Running: Full build (cold)...
set "StartTime=%time%"
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%RepoRoot%eng\common\build.ps1""" -restore -build -configuration Debug -v quiet" > nul 2>&1
set "EndTime=%time%"
call :CalcDuration "%StartTime%" "%EndTime%" Duration
echo   Full build (cold): %Duration%
echo Full build (cold): %Duration% >> "%ResultsFile%"

REM ============================================================================
REM Benchmark 2: Full build (incremental - no changes)
REM ============================================================================
echo [3/8] Running: Full build (incremental)...
set "StartTime=%time%"
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%RepoRoot%eng\common\build.ps1""" -restore -build -configuration Debug -v quiet" > nul 2>&1
set "EndTime=%time%"
call :CalcDuration "%StartTime%" "%EndTime%" Duration
echo   Full build (incremental): %Duration%
echo Full build (incremental): %Duration% >> "%ResultsFile%"

REM Clean for minimal builds
echo [4/8] Cleaning for minimal build tests...
call "%RepoRoot%build.cmd" -clean > nul 2>&1

REM ============================================================================
REM Benchmark 3: Minimal build with bootstrap (cold)
REM ============================================================================
echo [5/8] Running: Minimal build with bootstrap (cold)...
set "StartTime=%time%"
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%RepoRoot%eng\common\build.ps1""" -restore -build -configuration Debug -v quiet /p:CreateBootstrap=true /p:Projects=%RepoRoot%MSBuild.Minimal.slnf" > nul 2>&1
set "EndTime=%time%"
call :CalcDuration "%StartTime%" "%EndTime%" Duration
echo   Minimal build with bootstrap (cold): %Duration%
echo Minimal build with bootstrap (cold): %Duration% >> "%ResultsFile%"

REM ============================================================================
REM Benchmark 4: Minimal build with bootstrap (incremental)
REM ============================================================================
echo [6/8] Running: Minimal build with bootstrap (incremental)...
set "StartTime=%time%"
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%RepoRoot%eng\common\build.ps1""" -restore -build -configuration Debug -v quiet /p:CreateBootstrap=true /p:Projects=%RepoRoot%MSBuild.Minimal.slnf" > nul 2>&1
set "EndTime=%time%"
call :CalcDuration "%StartTime%" "%EndTime%" Duration
echo   Minimal build with bootstrap (incremental): %Duration%
echo Minimal build with bootstrap (incremental): %Duration% >> "%ResultsFile%"

REM ============================================================================
REM Benchmark 5: Minimal build without bootstrap (incremental)
REM ============================================================================
echo [7/8] Running: Minimal build without bootstrap (incremental)...
set "StartTime=%time%"
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%RepoRoot%eng\common\build.ps1""" -restore -build -configuration Debug -v quiet /p:CreateBootstrap=false /p:Projects=%RepoRoot%MSBuild.Minimal.slnf" > nul 2>&1
set "EndTime=%time%"
call :CalcDuration "%StartTime%" "%EndTime%" Duration
echo   Minimal build without bootstrap (incremental): %Duration%
echo Minimal build without bootstrap (incremental): %Duration% >> "%ResultsFile%"

REM ============================================================================
REM Benchmark 6: Minimal build .NET Core only (incremental)
REM ============================================================================
echo [8/8] Running: Minimal build .NET Core only (incremental)...
set "StartTime=%time%"
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%RepoRoot%eng\common\build.ps1""" -restore -build -configuration Debug -v quiet /p:CreateBootstrap=false /p:Projects=%RepoRoot%MSBuild.Minimal.slnf /p:TargetFrameworks=net10.0" > nul 2>&1
set "EndTime=%time%"
call :CalcDuration "%StartTime%" "%EndTime%" Duration
echo   Minimal build .NET Core only (incremental): %Duration%
echo Minimal build .NET Core only (incremental): %Duration% >> "%ResultsFile%"

echo. >> "%ResultsFile%"
echo ============================================================================ >> "%ResultsFile%"

echo.
echo ============================================================================
echo  Benchmark Complete!
echo ============================================================================
echo.
echo  Results saved to: %ResultsFile%
echo.
type "%ResultsFile%"
echo.

exit /b 0

:CalcDuration
REM Calculate duration between two times
REM Usage: call :CalcDuration "StartTime" "EndTime" ResultVar
setlocal EnableDelayedExpansion

set "Start=%~1"
set "End=%~2"

REM Parse start time
for /f "tokens=1-4 delims=:., " %%a in ("%Start%") do (
    set /a "StartSec=(((%%a*60)+%%b)*60+%%c)"
)

REM Parse end time
for /f "tokens=1-4 delims=:., " %%a in ("%End%") do (
    set /a "EndSec=(((%%a*60)+%%b)*60+%%c)"
)

set /a "DiffSec=EndSec-StartSec"
if !DiffSec! lss 0 set /a "DiffSec+=86400"

set /a "Minutes=DiffSec/60"
set /a "Seconds=DiffSec%%60"

if !Minutes! lss 10 set "Minutes=0!Minutes!"
if !Seconds! lss 10 set "Seconds=0!Seconds!"

endlocal & set "%~3=%Minutes%:%Seconds%"
goto :eof
