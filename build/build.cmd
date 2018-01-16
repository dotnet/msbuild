@echo off
powershell -ExecutionPolicy ByPass %~dp0build.ps1 -build -sign -pack -ci %*
exit /b %ErrorLevel%