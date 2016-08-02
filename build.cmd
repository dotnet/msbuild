@echo off
setlocal enabledelayedexpansion

set DeveloperCommandPrompt=%VS140COMNTOOLS%\VsDevCmd.bat

if not exist "%DeveloperCommandPrompt%" (
  echo In order to build this repository, you need Visual Studio 2015 installed.
  echo.
  echo Visit this page to download:
  echo.
  echo https://go.microsoft.com/fwlink/?LinkId=691978&clcid=0x409
  exit /b 1
)

call "%DeveloperCommandPrompt%" || goto :BuildFailed

msbuild %~dp0core-sdk.sln /nologo /v:m /m /nodereuse:false
IF %ERRORLEVEL% NEQ 0 (
    echo Build failed, exiting with %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo.
echo Build completed successfully
exit /b 0

:BuildFailed
echo Build failed with ERRORLEVEL %ERRORLEVEL%
exit /b 1
