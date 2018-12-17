@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0common\cibuild.cmd""" %*"
exit /b %ErrorLevel%
