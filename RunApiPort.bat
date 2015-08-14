@echo off
setlocal
set ANALYSIS_PATH=%~dp0bin\Windows_NT\Port-Progress
%~dp0ApiPort\ApiPort.exe analyze -f %ANALYSIS_PATH%\Microsoft.Build.dll -f %ANALYSIS_PATH%\Microsoft.Build.Framework.dll --target "ASP.NET 5, Version=1.0"