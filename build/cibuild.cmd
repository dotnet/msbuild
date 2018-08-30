@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass "& '%~dp0build.ps1'" -build -bootstrap -pack -ci -prepareMachine %*
exit /b %ErrorLevel%