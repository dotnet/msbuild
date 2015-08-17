@echo off
setlocal

msbuild %~dp0src\MSBuild.sln /p:Configuration=Port-Progress

set ANALYSIS_PATH=%~dp0bin\Windows_NT\Port-Progress
%~dp0ApiPort\ApiPort.exe analyze -f %ANALYSIS_PATH%\Microsoft.Build.dll -f %ANALYSIS_PATH%\Microsoft.Build.Framework.dll -f %ANALYSIS_PATH%\Microsoft.Build.Tasks.Core.dll -f %ANALYSIS_PATH%\Microsoft.Build.Utilities.Core.dll -f %ANALYSIS_PATH%\MSBuild.exe --target "ASP.NET 5, Version=1.0"