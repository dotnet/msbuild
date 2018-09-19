@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0Build.ps1""" -restore -publishBuildAssets %*"
exit /b %ErrorLevel%