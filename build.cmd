@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass "%~dp0build\build.ps1" -build %*
exit /b %ErrorLevel%
