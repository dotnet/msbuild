@echo off

call %~dp0\EnsureWebSdkEnv.cmd

REM Copy the files required for signing
xcopy \\aspnetci\share\tools\Microsoft.Web.MsBuildTasks2.dll %WebSdkTools% /y /C
xcopy \\aspnetci\share\tools\7za.exe %WebSdkTools% /y /C
xcopy \\aspnetci\share\tools\MIcrosoft.NET.Sdk.Web.Sign.targets %WebSdkTools% /y /C
xcopy \\aspnetci\share\tools\WebDeploy\* %WebSdkTools%\WebDeploy\* /y /C /e /s /f

REM run restore
call dotnet restore %WebSdkRoot%\Microsoft.Net.Sdk.Web.Sln /p:SkipInvalidConfigurations=true;configuration=Release
if errorlevel 1 GOTO ERROR

REM run build
call dotnet build %WebSdkRoot%\src\Publish\Microsoft.NET.Sdk.Publish.Tasks\Microsoft.NET.Sdk.Publish.Tasks.csproj /p:SkipInvalidConfigurations=true;configuration=Release
if errorlevel 1 GOTO ERROR

call %WebSdkRoot%\tools\InstallUpdates.cmd

REM run tests
call dotnet build %WebSdkRoot%\test\Publish\Microsoft.NET.Sdk.Publish.Tasks.Tests\Microsoft.NET.Sdk.Publish.Tasks.Tests.csproj /p:SkipInvalidConfigurations=true;configuration=Release
if errorlevel 1 GOTO ERROR

call dotnet test %WebSdkRoot%\test\Publish\Microsoft.NET.Sdk.Publish.Tasks.Tests\Microsoft.NET.Sdk.Publish.Tasks.Tests.csproj /p:SkipInvalidConfigurations=true;configuration=Release
if errorlevel 1 GOTO ERROR

msbuild %WebSdkRoot%\build.proj /p:configuration=Release;SkipInvalidConfigurations=true /t:Build;Sign
if errorlevel 0 exit /b 0

:ERROR
endlocal
exit /b 1