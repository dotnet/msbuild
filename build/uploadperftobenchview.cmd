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
if /I not "%runType%" == "private" if /I not "%runType%" == "rolling" (
    echo EnvVar runType should be set; exiting...
    exit /b 1)
if /I "%runType%" == "private" if "%BenchviewCommitName%" == "" (
    echo EnvVar BenchviewCommitName should be set; exiting...
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

powershell -NoProfile -NoLogo wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile "%perfWorkingDirectory%\nuget.exe"

if exist "%perfWorkingDirectory%\Microsoft.BenchView.JSONFormat" rmdir /s /q "%perfWorkingDirectory%\Microsoft.BenchView.JSONFormat"
"%perfWorkingDirectory%\nuget.exe" install Microsoft.BenchView.JSONFormat -Source http://benchviewtestfeed.azurewebsites.net/nuget -OutputDirectory "%perfWorkingDirectory%" -Prerelease -ExcludeVersion

REM Do this here to remove the origin but at the front of the branch name as this is a problem for BenchView
if "%GIT_BRANCH:~0,7%" == "origin/" (set GIT_BRANCH_WITHOUT_ORIGIN=%GIT_BRANCH:origin/=%) else (set GIT_BRANCH_WITHOUT_ORIGIN=%GIT_BRANCH%)

for /f %%x in ('powershell -NoProfile -NoLogo -Command "Get-Date -Date (Get-Date).ToUniversalTime() -UFormat '%%Y-%%m-%%dT%%H:%%M:%%SZ'"') do (set timeStamp=%%x)

set benchViewName=SDK perf %OS% %architecture% %configuration% %runType% %GIT_BRANCH_WITHOUT_ORIGIN%
if /I "%runType%" == "private" (set benchViewName=%benchViewName% %BenchviewCommitName%)
if /I "%runType%" == "rolling" (set benchViewName=%benchViewName% %GIT_COMMIT%)
echo BenchViewName: "%benchViewName%"

echo Creating: "%perfWorkingDirectory%\submission-metadata.json"
%pythonCmd% "%perfWorkingDirectory%\Microsoft.BenchView.JSONFormat\tools\submission-metadata.py" --name "%benchViewName%" --user-email "dotnet-bot@microsoft.com" -o "%perfWorkingDirectory%\submission-metadata.json"

echo Creating: "%perfWorkingDirectory%\build.json"
%pythonCmd% "%perfWorkingDirectory%\Microsoft.BenchView.JSONFormat\tools\build.py" git --branch "%GIT_BRANCH_WITHOUT_ORIGIN%" --type "%runType%" --source-timestamp "%timeStamp%" -o "%perfWorkingDirectory%\build.json"

echo Creating: "%perfWorkingDirectory%\machinedata.json"
%pythonCmd% "%perfWorkingDirectory%\Microsoft.BenchView.JSONFormat\tools\machinedata.py" -o "%perfWorkingDirectory%\machinedata.json"

echo Creating: "%perfWorkingDirectory%\measurement.json"
pushd "%perfWorkingDirectory%"
for /f "tokens=*" %%a in ('dir /b/a-d *.xml') do (
    echo Processing: "%%a"
    %pythonCmd% "%perfWorkingDirectory%\Microsoft.BenchView.JSONFormat\tools\measurement.py" xunitscenario "%%a" --better desc --drop-first-value --append -o "%perfWorkingDirectory%\measurement.json"
)
popd

echo Creating: "%perfWorkingDirectory%\submission.json"
%pythonCmd% "%perfWorkingDirectory%\Microsoft.BenchView.JSONFormat\tools\submission.py" "%perfWorkingDirectory%\measurement.json" ^
                    --build "%perfWorkingDirectory%\build.json" ^
                    --machine-data "%perfWorkingDirectory%\machinedata.json" ^
                    --metadata "%perfWorkingDirectory%\submission-metadata.json" ^
                    --group "SDK Perf Tests" ^
                    --type "%runType%" ^
                    --config-name "%configuration%" ^
                    --config Configuration "%configuration%" ^
                    --config OS "%OS%" ^
                    --architecture "%architecture%" ^
                    --machinepool "perfsnake" ^
                    -o "%perfWorkingDirectory%\submission.json"

echo Uploading: "%perfWorkingDirectory%\submission.json"
%pythonCmd% "%perfWorkingDirectory%\Microsoft.BenchView.JSONFormat\tools\upload.py" "%perfWorkingDirectory%\submission.json" --container coreclr

exit /b %ErrorLevel%
