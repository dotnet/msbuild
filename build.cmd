@echo off

setlocal EnableDelayedExpansion

REM UTC Timestamp of the last commit is used as the build number. This is for easy synchronization of build number between Windows, OSX and Linux builds.
REM Using powershell is way easier to retrive and format the timestamp in any way we want.
set LAST_COMMIT_TIMESTAMP=powershell -Command "& { $timestamp = git log -1 --format=%%ct ; $origin = New-Object -Type DateTime -ArgumentList 1970, 1, 1, 0, 0, 0, 0; $commitTime = $origin.AddSeconds($timestamp); echo $commitTime.ToString(\"yyyyMMdd-HHmmss\");}"

for /f %%i in ('%LAST_COMMIT_TIMESTAMP%') do set DOTNET_BUILD_VERSION=0.0.1-alpha-%%i

where dnvm
if %ERRORLEVEL% neq 0 (
    @powershell -NoProfile -ExecutionPolicy unrestricted -Command "&{$Branch='dev';iex ((new-object net.webclient).DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1'))}"
    set PATH=!PATH!;!USERPROFILE!\.dnx\bin
    set DNX_HOME=!USERPROFILE!\.dnx
    goto continue
)

:continue
call %~dp0scripts/bootstrap.cmd
if %errorlevel% neq 0 exit /b %errorlevel%

call %~dp0scripts/package.cmd
if %errorlevel% neq 0 exit /b %errorlevel%
