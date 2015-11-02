@echo off

REM This trick gets the absolute path from a relative path
pushd %~dp0..
set REPOROOT=%CD%
popd

set RID=win7-x64
set STAGE2_DIR=%REPOROOT%\artifacts\%RID%\stage2
set DESTINATION=%USERPROFILE%\.dotnet\sdks\dotnet-win-x64.0.0.1-dev

echo Junctioning %STAGE2_DIR% to %DESTINATION%

if not exist %DESTINATION% goto link
echo Removing old junction %DESTINATION%
rd /s /q %DESTINATION%

:link
mklink /J %DESTINATION% %STAGE2_DIR%
