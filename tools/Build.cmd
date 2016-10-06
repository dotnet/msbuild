@echo off

call %~dp0\EnsurePublishEnv.cmd

copy \\aspnetci\share\tools\Microsoft.Web.MsBuildTasks2.dll %PublishTools% /y
copy \\aspnetci\share\tools\7za.exe %PublishTools% /y

dotnet restore3 %PublishRoot%\src\Microsoft.NETCore.Sdk.Publish.Tasks\Microsoft.NETCore.Sdk.Publish.Tasks.csproj /p:SkipInvalidConfigurations=true;configuration=Release
dotnet build3 %PublishRoot%\src\Microsoft.NETCore.Sdk.Publish.Tasks\Microsoft.NETCore.Sdk.Publish.Tasks.csproj /p:SkipInvalidConfigurations=true;configuration=Release
msbuild %PublishRoot%\dirs.proj /p:configuration=Release;SkipInvalidConfigurations=true /t:Build;Sign