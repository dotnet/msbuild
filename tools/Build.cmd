@echo off

call %~dp0\EnsureWebSdkEnv.cmd

copy \\aspnetci\share\tools\Microsoft.Web.MsBuildTasks2.dll %WebSdkTools% /y
copy \\aspnetci\share\tools\7za.exe %WebSdkTools% /y

call dotnet restore %WebSdkRoot%\Microsoft.Net.Sdk.Web.Sln /p:SkipInvalidConfigurations=true;configuration=Release
if errorlevel 1 GOTO ERROR

call msbuild %WebSdkRoot%\src\Publish\Microsoft.NET.Sdk.Publish.Tasks\Microsoft.NET.Sdk.Publish.Tasks.csproj /t:build /p:SkipInvalidConfigurations=true;configuration=Release
if errorlevel 1 GOTO ERROR

call dotnet build %WebSdkRoot%\src\Publish\Microsoft.NETCore.Sdk.Publish.Tasks\Microsoft.NETCore.Sdk.Publish.Tasks.csproj /p:SkipInvalidConfigurations=true;configuration=Release
if errorlevel 1 GOTO ERROR

call dotnet build %WebSdkRoot%\test\Publish\Microsoft.NET.Sdk.Publish.Tasks.Tests\Microsoft.NET.Sdk.Publish.Tasks.Tests.csproj /p:SkipInvalidConfigurations=true;configuration=Release;TargetFramework=netcoreapp1.0
if errorlevel 1 GOTO ERROR

call dotnet test %WebSdkRoot%\test\Publish\Microsoft.NET.Sdk.Publish.Tasks.Tests\Microsoft.NET.Sdk.Publish.Tasks.Tests.csproj /p:SkipInvalidConfigurations=true;configuration=Release;TargetFramework=netcoreapp1.0
if errorlevel 1 GOTO ERROR

msbuild %WebSdkRoot%\dirs.proj /p:configuration=Release;SkipInvalidConfigurations=true /t:Build;Sign

:ERROR
endlocal
exit /b 0