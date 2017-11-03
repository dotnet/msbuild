@echo off

if not defined DOTNET_INSTALL_DIR (
    set DOTNET_INSTALL_DIR=%LocalAppData%\Microsoft\dotnet
)

if not defined DOTNET_VERSION (
    set DOTNET_VERSION=2.0.1-servicing-006924
)

set "WebSdkRoot=%~dp0"
set "WebSdkRoot=%WebSdkRoot:~0,-7%"
set "WebSdkBin=%WebSdkRoot%\bin\"
set "WebSdkIntermediate=%WebSdkRoot%\obj\"
set "WebSdkReferences=%WebSdkRoot%\references\"
set "WebSdkSource=%WebSdkRoot%\src\"
set "WebSdkTools=%WebSdkRoot%\tools\"

set "PATH=%PATH%;%WebSdkTools%"