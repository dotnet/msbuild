@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0Build.ps1""" -restore -build -test -sign -pack -ci %*"
exit /b %ErrorLevel%
