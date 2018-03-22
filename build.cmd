@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0build\build.ps1""" -build -restore -log %*"
exit /b %ErrorLevel%
