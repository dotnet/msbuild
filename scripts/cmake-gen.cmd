@echo off

REM Generate project files for the coreclr host
set CMAKE_OUTPUT=%~dp0..\src\corehost\cmake
if not exist %CMAKE_OUTPUT% mkdir %CMAKE_OUTPUT%
pushd %CMAKE_OUTPUT%
cmake .. -G "Visual Studio 14 2015 Win64"
if %errorlevel% neq 0 exit /b %errorlevel%
popd

