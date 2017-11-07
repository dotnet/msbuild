@echo off

call "%~dp0\EnsureWebSdkEnv.cmd"

REM Copy the files required for signing
xcopy \\aspnetci\share\tools\websdk\Microsoft.Web.MsBuildTasks2.dll "%WebSdkTools%" /y /C
xcopy \\aspnetci\share\tools\websdk\7za.exe "%WebSdkTools%" /y /C
xcopy \\aspnetci\share\tools\websdk\Microsoft.NET.Sdk.Web.Sign.targets "%WebSdkTools%" /y /C
xcopy \\aspnetci\share\tools\websdk\WebDeploy\* "%WebSdkTools%\WebDeploy\*" /y /C /e /s /f

msbuild "%WebSdkRoot%\build.proj" /p:configuration=Release /p:SkipInvalidConfigurations=true /t:Build;Sign
if errorlevel 0 exit /b 0

:ERROR
endlocal
exit /b 1