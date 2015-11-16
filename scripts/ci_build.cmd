@echo off

pushd %~dp0..
set DOTNET_INSTALL_DIR=%CD%\artifacts\win7-x64\stage0
popd
CALL %~dp0..\build.cmd %*

exit /b %errorlevel%
