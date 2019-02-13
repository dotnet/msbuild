@echo off
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\common\Build.ps1""" -build -restore -pack -test %*"
exit /b %ErrorLevel%