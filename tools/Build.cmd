@echo off

call %~dp0\EnsureWebSdkEnv.cmd

REM Copy the files required for signing
xcopy \\aspnetci\share\tools\Microsoft.Web.MsBuildTasks2.dll %WebSdkTools% /y /C
xcopy \\aspnetci\share\tools\7za.exe %WebSdkTools% /y /C
xcopy \\aspnetci\share\tools\MIcrosoft.NET.Sdk.Web.Sign.targets %WebSdkTools% /y /C

call dotnet restore %WebSdkRoot%\Microsoft.Net.Sdk.Web.Sln /p:SkipInvalidConfigurations=true;configuration=Release
if errorlevel 1 GOTO ERROR

REM NuGet is not restoring the project for net46 during the solution restore. Also, NuGet throws a null ref exception while calling dotnet restore. Hence, work-around is to call dotnet msbuild with target restore.
call dotnet msbuild %WebSdkRoot%\src\Publish\Microsoft.NET.Sdk.Publish.Tasks\Microsoft.NET.Sdk.Publish.Tasks.csproj /p:SkipInvalidConfigurations=true;configuration=Release;TargetFramework=net46 /t:Restore
if errorlevel 1 GOTO ERROR

call dotnet build %WebSdkRoot%\src\Publish\Microsoft.NET.Sdk.Publish.Tasks\Microsoft.NET.Sdk.Publish.Tasks.csproj /p:SkipInvalidConfigurations=true;configuration=Release
if errorlevel 1 GOTO ERROR


REM Copy the built output to the CLI folder for testing
xcopy %WebSdkSource%\Publish\Microsoft.NET.Sdk.Publish.Targets\Sdk.props  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Publish\Sdk\ /y /C
xcopy %WebSdkSource%\Publish\Microsoft.NET.Sdk.Publish.Targets\Sdk.targets  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Publish\Sdk\ /y /C

xcopy %WebSdkSource%\Publish\Microsoft.NET.Sdk.Publish.Targets\netstandard1.0\*  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Publish\build\netstandard1.0\* /y /C /e /s /f

xcopy %WebSdkbin%\Release\net46\Microsoft.NET.Sdk.Publish.Tasks.dll %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Publish\tools\net46\ /y /C
xcopy %WebSdkbin%\Release\netstandard1.3\Microsoft.NET.Sdk.Publish.Tasks.dll %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Publish\tools\netcoreapp1.0\ /y /C

REM Tests
REM NuGet is not restoring the project for net46 during the solution restore. Also, NuGet throws a null ref exception while calling dotnet restore. Hence, work-around is to call dotnet msbuild with target restore.
call dotnet msbuild %WebSdkRoot%\test\Publish\Microsoft.NET.Sdk.Publish.Tasks.Tests\Microsoft.NET.Sdk.Publish.Tasks.Tests.csproj /p:SkipInvalidConfigurations=true;configuration=Release;TargetFramework=net46 /t:Restore
if errorlevel 1 GOTO ERROR

call dotnet build %WebSdkRoot%\test\Publish\Microsoft.NET.Sdk.Publish.Tasks.Tests\Microsoft.NET.Sdk.Publish.Tasks.Tests.csproj /p:SkipInvalidConfigurations=true;configuration=Release
if errorlevel 1 GOTO ERROR

REM dotnet test does not report errors if a test fails and target framework is not passed to the dotnet test command. dotnet test silently fails. Hence, calling the dotnet test 2 times, one for each framework.
call dotnet test %WebSdkRoot%\test\Publish\Microsoft.NET.Sdk.Publish.Tasks.Tests\Microsoft.NET.Sdk.Publish.Tasks.Tests.csproj /p:SkipInvalidConfigurations=true;configuration=Release;TargetFramework=net46
if errorlevel 1 GOTO ERROR

call dotnet test %WebSdkRoot%\test\Publish\Microsoft.NET.Sdk.Publish.Tasks.Tests\Microsoft.NET.Sdk.Publish.Tasks.Tests.csproj /p:SkipInvalidConfigurations=true;configuration=Release;TargetFramework=netcoreapp1.0
if errorlevel 1 GOTO ERROR

msbuild %WebSdkRoot%\build.proj /p:configuration=Release;SkipInvalidConfigurations=true /t:Build;Sign
if errorlevel 0 exit /b 0

:ERROR
endlocal
exit /b 1