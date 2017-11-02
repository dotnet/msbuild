@echo off

REM Copy Publish Sdks
xcopy %WebSdkSource%\Publish\Microsoft.NET.Sdk.Publish.Targets\Sdk.props  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Publish\Sdk\ /y /C
xcopy %WebSdkSource%\Publish\Microsoft.NET.Sdk.Publish.Targets\Sdk.targets  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Publish\Sdk\ /y /C

REM Copy Project System Sdks
xcopy %WebSdkSource%\ProjectSystem\Microsoft.NET.Sdk.Web.ProjectSystem.Targets\Sdk.props  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Web.ProjectSystem\Sdk\ /y /C
xcopy %WebSdkSource%\ProjectSystem\Microsoft.NET.Sdk.Web.ProjectSystem.Targets\Sdk.targets  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Web.ProjectSystem\Sdk\ /y /C

REM Copy Web Sdks
xcopy %WebSdkSource%\Web\Microsoft.NET.Sdk.Web.Targets\Sdk.props  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Web\Sdk\ /y /C
xcopy %WebSdkSource%\Web\Microsoft.NET.Sdk.Web.Targets\Sdk.targets  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Web\Sdk\ /y /C

REM Copy Targets
xcopy %WebSdkSource%\Publish\Microsoft.NET.Sdk.Publish.Targets\netstandard1.0\*  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Publish\build\netstandard1.0\* /y /C /e /s /f
xcopy %WebSdkSource%\ProjectSystem\Microsoft.NET.Sdk.Web.ProjectSystem.Targets\netstandard1.0\*  %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Web.ProjectSystem\build\netstandard1.0\* /y /C /e /s /f

REM Copy Tasks
xcopy %WebSdkbin%\Release\net46\win7-x86\Microsoft.NET.Sdk.Publish.Tasks.dll %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Publish\tools\net46\ /y /C
xcopy %WebSdkbin%\Release\netstandard1.3\Microsoft.NET.Sdk.Publish.Tasks.dll %DOTNET_INSTALL_DIR%\Sdk\%DOTNET_VERSION%\Sdks\Microsoft.NET.Sdk.Publish\tools\netcoreapp1.0\ /y /C

