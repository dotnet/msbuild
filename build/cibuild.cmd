@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass "& '%~dp0build.ps1'" -build -pack -sign -ci -prepareMachine %*
exit /b %ErrorLevel%