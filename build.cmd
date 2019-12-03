@echo off
set CACHEDMSBUILDDEBUGONSTART=%MSBUILDDEBUGONSTART%
if %MSBUILDDEBUGONSTART% == 0 goto build
set /p ans=Did you mean to have MSBUILDDEBUGONSTART equal %MSBUILDDEBUGONSTART%? (y/n) 
if %ans% == y goto build
set MSBUILDDEBUGONSTART=0
:build
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0eng\common\build.ps1""" -build -restore %*"
set MSBUILDDEBUGONSTART=%CACHEDMSBUILDDEBUGONSTART%
exit /b %ErrorLevel%
