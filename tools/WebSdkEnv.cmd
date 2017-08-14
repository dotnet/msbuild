@echo off

if defined ProgramFiles(x86) (
    set "WebSdkProgramFiles=%ProgramFiles(x86)%"
) else (
    set "WebSdkProgramFiles=%ProgramFiles%"
)

if not defined DNX_PACKAGES (
    set DNX_PACKAGES=%~dp0\..\packages
)

if not defined DOTNET_INSTALL_DIR (
    set DOTNET_INSTALL_DIR=%LocalAppData%\Microsoft\dotnet
)

if not defined DOTNET_VERSION (
    set DOTNET_VERSION=2.0.1-servicing-006924
)

if exist "%WebSdkProgramFiles%\Microsoft Visual Studio\VS15Preview\MSBuild\15.0\Bin\MSBuild.exe" (
    set "WebSdkVSVersion=15.0"
    set WebSdkMSBuildPath="%WebSdkProgramFiles%\Microsoft Visual Studio\VS15Preview\MSBuild\15.0\Bin"
) else (
    set "WebSdkVSVersion=14.0"
    set WebSdkMSBuildPath="%WebSdkProgramFiles%\MSBuild\14.0\Bin"
)

set "WebSdkRoot=%~dp0"
set "WebSdkRoot=%WebSdkRoot:~0,-7%"
set "WebSdkBin=%WebSdkRoot%\bin\"
set "WebSdkIntermediate=%WebSdkRoot%\obj\"
set "WebSdkReferences=%WebSdkRoot%\references\"
set "WebSdkSource=%WebSdkRoot%\src\"
set "WebSdkTools=%WebSdkRoot%\tools\"

set "PATH=%PATH%;%WebSdkMSBuildPath%"
set "PATH=%PATH%;%WebSdkTools%"