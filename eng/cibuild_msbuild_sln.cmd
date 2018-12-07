@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0common\cibuild.cmd""" /p:Projects="""%~dp0../MSBuild.sln""" %*"
exit /b %ErrorLevel%
