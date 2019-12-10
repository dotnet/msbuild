@echo off
setlocal
if defined MSBUILDDEBUGONSTART_HARD goto build
if not defined MSBUILDDEBUGONSTART goto build
if %MSBUILDDEBUGONSTART% == 0 goto build
set MSBUILDDEBUGONSTART=
echo To debug the build, define a value for MSBUILDDEBUGONSTART_HARD.
:build
powershell -NoLogo -NoProfile -ExecutionPolicy ByPass -Command "& """%~dp0eng\common\build.ps1""" -build -restore %*"
exit /b %ErrorLevel%
