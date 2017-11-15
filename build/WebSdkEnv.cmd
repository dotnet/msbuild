@echo off

if not defined DOTNET_INSTALL_DIR (
    set "DOTNET_INSTALL_DIR=%LocalAppData%\Microsoft\dotnet"
)

if not defined DOTNET_VERSION (
    set DOTNET_VERSION=2.0.1-servicing-006924
)

set "WebSdkRoot=%~dp0"
set "WebSdkRoot=%WebSdkRoot:~0,-7%"
set "WebSdkReferences=%WebSdkRoot%\references\"
set "WebSdkSource=%WebSdkRoot%\src\"
set "WebSdkBuild=%WebSdkRoot%\build\"
set "WebSdkPublishBin=%WebSdkRoot%\src\Publish\Microsoft.NET.Sdk.Publish.Tasks\bin\"

set "PATH=%PATH%;%WebSdkBuild%"