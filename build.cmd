@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0eng\common\build.ps1""" -build -restore -p:Projects="""%~dp0MSBuild.sln""" %*"
exit /b %ErrorLevel%
