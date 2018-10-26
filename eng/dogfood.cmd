@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -NoExit -Command "& """%~dp0dogfood.ps1""" %*"
exit /b %ErrorLevel%
