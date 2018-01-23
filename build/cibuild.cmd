@echo off
powershell -ExecutionPolicy ByPass "%~dp0build.ps1" -build -pack -sign -ci -prepareMachine %*
exit /b %ErrorLevel%