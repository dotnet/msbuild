@echo off

set DOTNET_INSTALL_DIR=%~dp0..\artifacts\win7-x64\stage0
CALL %~dp0..\build.cmd %*

exit /b %errorlevel%
