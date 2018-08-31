@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass "& '%~dp0build.ps1'" -build -bootstrap -pack -sign -ci -prepareMachine %*
exit /b %ErrorLevel%
