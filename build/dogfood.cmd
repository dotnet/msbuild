@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -NoExit %~dp0Build.ps1 -dogfood %*
exit /b %ErrorLevel%
