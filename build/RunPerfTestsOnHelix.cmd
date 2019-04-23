@echo on
setlocal EnableDelayedExpansion

 if "%BV_UPLOAD_SAS_TOKEN%" == "" (
    echo EnvVar BV_UPLOAD_SAS_TOKEN should be set; exiting...
    exit /b 1)
if "%HELIX_CORRELATION_PAYLOAD%" == "" (
    echo EnvVar HELIX_CORRELATION_PAYLOAD should be set; exiting...
    exit /b 1)

set configuration=%1
set PerfIterations=%2
set GIT_COMMIT=%3
set GIT_BRANCH=%4
set runType=%5

REM  Since dotnet.exe was locked; we exclude it from the helix-payload.
REM    Run a restore to re-install the SDK.
echo "Running a 'build.ps1 -restore'"
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%HELIX_CORRELATION_PAYLOAD%\eng\common\build.ps1""" -configuration %configuration% -restore"

REM  Since the Microsoft.NET.PerformanceTests.runtimeconfig.dev.json has a hard-coded path to the NuGet root, we exclude all test harnesses from the helix-payload.
REM    Build the PerformanceTests harness on the Helix machine.
echo "Building:'Microsoft.NET.PerformanceTests.dll'"
%HELIX_CORRELATION_PAYLOAD%\.dotnet\dotnet.exe msbuild %HELIX_CORRELATION_PAYLOAD%\src\Tests\Microsoft.NET.PerformanceTests\Microsoft.NET.PerformanceTests.csproj /t:build /p:configuration=%configuration% /p:NUGET_PACKAGES=%HELIX_CORRELATION_PAYLOAD%\.packages

REM  Run the performance tests and collect performance data.
echo "Running the performance tests and collecting data"
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%HELIX_CORRELATION_PAYLOAD%\eng\common\build.ps1""" -configuration %configuration% -ci -msbuildEngine dotnet -performanceTest /p:PerfIterations=%PerfIterations%"
IF %ERRORLEVEL% GTR 0 exit %ERRORLEVEL%
echo "Performance tests completed"

REM  Upload the performance data to BenchView.
set perfWorkingDirectory=%HELIX_CORRELATION_PAYLOAD%\artifacts\TestResults\%configuration%\Performance
set architecture=%PROCESSOR_ARCHITECTURE%
if "%PROCESSOR_ARCHITECTURE%" == "AMD64" (set architecture=x64)

echo "Uploading data to Benchview: uploadperftobenchview.cmd"
pushd %HELIX_CORRELATION_PAYLOAD%
%HELIX_CORRELATION_PAYLOAD%\build\uploadperftobenchview.cmd
IF %ERRORLEVEL% GTR 0 exit %ERRORLEVEL%
echo "Upload to Benchview completed"

exit /b %ErrorLevel%
