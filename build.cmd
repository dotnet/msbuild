@echo off
xcopy \\aspnetci\share\tools\websdk\WebDeploy\* "%~dp0build\WebDeploy\*" /y /C /e /s /f
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0eng\common\Build.ps1""" -build -restore -pack -test %*"
exit /b %ErrorLevel%