@echo on
setlocal EnableDelayedExpansion

REM The intent of this script is upload produced performance results to BenchView in a CI context.
REM    There is no support for running this script in a dev environment.

if "%perfWorkingDirectory%" == "" (
    echo EnvVar perfWorkingDirectory should be set; exiting...
    exit /b %1)
if "%configuration%" == "" (
    echo EnvVar configuration should be set; exiting...
    exit /b 1)
if "%architecture%" == "" (
    echo EnvVar architecture should be set; exiting...
    exit /b 1)
if "%OS%" == "" (
    echo EnvVar OS should be set; exiting...
    exit /b 1)
if "%TestFullMSBuild%" == "" (
    set TestFullMSBuild=false
    )
if /I not "%runType%" == "private" if /I not "%runType%" == "rolling" (
    echo EnvVar runType should be set; exiting...
    exit /b 1)
if /I "%runType%" == "private" if "%TestRunCommitName%" == "" (
    echo EnvVar TestRunCommitName should be set; exiting...
    exit /b 1)
if /I "%runType%" == "rolling" if "%GIT_COMMIT%" == "" (
    echo EnvVar GIT_COMMIT should be set; exiting...
    exit /b 1)
if "%GIT_BRANCH%" == "" (
    echo EnvVar GIT_BRANCH should be set; exiting...
    exit /b 1)
if not exist %perfWorkingDirectory%\nul ( 
    echo $perfWorkingDirectory does not exist; exiting...
    exit 1)

set pythonCmd=py
if exist "C:\Python35\python.exe" set pythonCmd=C:\Python35\python.exe

REM Do this here to remove the origin but at the front of the branch name
if "%GIT_BRANCH:~0,7%" == "origin/" (set GIT_BRANCH_WITHOUT_ORIGIN=%GIT_BRANCH:origin/=%) else (set GIT_BRANCH_WITHOUT_ORIGIN=%GIT_BRANCH%)

set TestRunName=SDK perf %OS% %architecture% %configuration% TestFullMSBuild-%TestFullMSBuild% %runType% %GIT_BRANCH_WITHOUT_ORIGIN%
if /I "%runType%" == "private" (set TestRunName=%TestRunName% %TestRunCommitName%)
if /I "%runType%" == "rolling" (set TestRunName=%TestRunName% %GIT_COMMIT%)
echo TestRunName: "%TestRunName%"

echo Creating and Uploading: "%perfWorkingDirectory%\submission.json"
%HELIX_CORRELATION_PAYLOAD%\.dotnet\dotnet.exe run^
 --project %HELIX_CORRELATION_PAYLOAD%\src\Tests\PerformanceTestsResultUploader\PerformanceTestsResultUploader.csproj^
 /p:configuration=%configuration%^
 /p:NUGET_PACKAGES=%HELIX_CORRELATION_PAYLOAD%\.packages --^
 --output "%perfWorkingDirectory%\submission.json"^
 --repository-root "%HELIX_CORRELATION_PAYLOAD%"^
 --sas "%PERF_COMMAND_UPLOAD_TOKEN%"

exit /b %ErrorLevel%
