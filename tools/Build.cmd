@echo off

call %~dp0\EnsureWebSdkEnv.cmd

REM Copy the files required for signing
xcopy \\aspnetci\share\tools\Microsoft.Web.MsBuildTasks2.dll %WebSdkTools% /y /C
xcopy \\aspnetci\share\tools\7za.exe %WebSdkTools% /y /C
xcopy \\aspnetci\share\tools\Microsoft.NET.Sdk.Web.Sign.targets %WebSdkTools% /y /C
xcopy \\aspnetci\share\tools\WebDeploy\* %WebSdkTools%\WebDeploy\* /y /C /e /s /f

call %WebSdkRoot%\tools\InstallUpdates.cmd

msbuild %WebSdkRoot%\build.proj /p:configuration=Release;SkipInvalidConfigurations=true /t:Build;Sign
if errorlevel 0 exit /b 0

:ERROR
endlocal
exit /b 1