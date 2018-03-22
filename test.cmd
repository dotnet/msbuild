@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0build\build.ps1""" -test %*"
exit /b %ErrorLevel%
