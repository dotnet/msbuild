@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -NoExit -Command "& """%~dp0Build.ps1""" -dogfood %*"
exit /b %ErrorLevel%
