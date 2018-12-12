@echo off
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0cibuild_bootstrapped_msbuild.ps1""" /p:Projects="""%~dp0../MSBuild.sln""" %*"
exit /b %ErrorLevel%
