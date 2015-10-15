@echo off

REM Build 'dotnet' using a version of 'dotnet' hosted on the DNX
REM The output of this is independent of DNX

where dnvm >nul 2>nul
if %errorlevel% == 0 goto have_dnvm

REM download dnvm
echo Installing dnvm (DNX is needed to bootstrap currently) ...
powershell -NoProfile -ExecutionPolicy unrestricted -Command "&{$Branch='dev';$wc=New-Object System.Net.WebClient;$wc.Proxy=[System.Net.WebRequest]::DefaultWebProxy;$wc.Proxy.Credentials=[System.Net.CredentialCache]::DefaultNetworkCredentials;Invoke-Expression ($wc.DownloadString('https://raw.githubusercontent.com/aspnet/Home/dev/dnvminstall.ps1'))}"

:have_dnvm
echo Installing and use-ing the latest CoreCLR x64 DNX ...
call dnvm install -u latest -r coreclr -arch x64 -alias dotnet_bootstrap
if errorlevel 1 goto fail

call dnvm use dotnet_bootstrap -r coreclr -arch x64
if errorlevel 1 goto fail

rd /s /q %~dp0artifacts\bootstrap
if errorlevel 1 goto fail

echo Running 'dnu restore' to restore packages for DNX-hosted projects
call dnu restore "%~dp0src\Microsoft.DotNet.Cli"
if errorlevel 1 goto fail

call dnu restore "%~dp0src\Microsoft.DotNet.Tools.Compiler"
if errorlevel 1 goto fail

call dnu restore "%~dp0src\Microsoft.DotNet.Tools.Publish"
if errorlevel 1 goto fail

echo Building basic dotnet tools using DNX-hosted version

echo Building dotnet.exe ...
call "%~dp0scripts\bootstrap\dotnet-publish" --framework dnxcore50 --runtime win7-x64 --output "%~dp0artifacts\bootstrap" "%~dp0src\Microsoft.DotNet.Cli"
if errorlevel 1 goto fail

echo Building dotnet-compile.exe ...
call "%~dp0scripts\bootstrap\dotnet-publish" --framework dnxcore50 --runtime win7-x64 --output "%~dp0artifacts\bootstrap" "%~dp0src\Microsoft.DotNet.Tools.Compiler"
if errorlevel 1 goto fail

echo Building dotnet-publish.exe ...
call "%~dp0scripts\bootstrap\dotnet-publish" --framework dnxcore50 --runtime win7-x64 --output "%~dp0artifacts\bootstrap" "%~dp0src\Microsoft.DotNet.Tools.Publish"
if errorlevel 1 goto fail

echo Bootstrapped 'dotnet' command is available in %~dp0artifacts\bootstrap
goto end

:fail
echo Bootstrapping failed...
exit /B 1

:end