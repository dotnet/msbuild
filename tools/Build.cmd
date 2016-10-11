@echo off

call %~dp0\EnsureWebSdkEnv.cmd

copy \\aspnetci\share\tools\Microsoft.Web.MsBuildTasks2.dll %WebSdkTools% /y
copy \\aspnetci\share\tools\7za.exe %WebSdkTools% /y

dotnet restore3 %WebSdkRoot%\src\Publish\Microsoft.NETCore.Sdk.Publish.Tasks\Microsoft.NETCore.Sdk.Publish.Tasks.csproj /p:SkipInvalidConfigurations=true;configuration=Release
dotnet build3 %WebSdkRoot%\src\Publish\Microsoft.NETCore.Sdk.Publish.Tasks\Microsoft.NETCore.Sdk.Publish.Tasks.csproj /p:SkipInvalidConfigurations=true;configuration=Release
msbuild %WebSdkRoot%\dirs.proj /p:configuration=Release;SkipInvalidConfigurations=true /t:Build;Sign