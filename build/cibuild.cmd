@echo off
REM -sign temporarily disabled
powershell -ExecutionPolicy ByPass %~dp0build.ps1 -build -pack -ci -prepareMachine %*
exit /b %ErrorLevel%