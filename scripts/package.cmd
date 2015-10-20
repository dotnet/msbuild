@echo off

REM PowerShell has access to Zipping tools
@powershell -NoLogo -NoProfile -ExecutionPolicy unrestricted -File "%~dp0package.ps1"
