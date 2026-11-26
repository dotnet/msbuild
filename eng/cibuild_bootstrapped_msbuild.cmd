@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0cibuild_bootstrapped_msbuild.ps1""" %*"
exit /b %ErrorLevel%
