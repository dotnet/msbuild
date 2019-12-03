@echo off
set CACHEDMSBUILDDEBUGONSTART=%MSBUILDDEBUGONSTART%
set MSBUILDDEBUGONSTART=0
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0eng\common\build.ps1""" -build -restore %*"
set MSBUILDDEBUGONSTART=%CACHEDMSBUILDDEBUGONSTART%
exit /b %ErrorLevel%
