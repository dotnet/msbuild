@echo off
setlocal

set _args=%*
if "%~1"=="-?" set _args=-help
if "%~1"=="/?" set _args=-help

if defined MSBUILDDEBUGONSTART_HARD goto build
if not defined MSBUILDDEBUGONSTART goto build
if %MSBUILDDEBUGONSTART% == 0 goto build
set MSBUILDDEBUGONSTART=
echo To debug the build, define a value for MSBUILDDEBUGONSTART_HARD.

:build
powershell -ExecutionPolicy ByPass -NoProfile -Command "& '%~dp0eng\build.ps1'" -restore -build %_args%
exit /b %ERRORLEVEL%
