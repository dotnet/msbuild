@echo off

CALL %~dp0..\build.cmd %*

exit /b %errorlevel%
