@if "%_echo%" neq "on" echo off
setlocal

set INIT_TOOLS_LOG=%~dp0init-tools.log
if [%PACKAGES_DIR%]==[] set PACKAGES_DIR=%~dp0packages\
if [%TOOLRUNTIME_DIR%]==[] set TOOLRUNTIME_DIR=%~dp0Tools
set DOTNET_PATH=%TOOLRUNTIME_DIR%\dotnetcli\
if [%DOTNET_CMD%]==[] set DOTNET_CMD=%DOTNET_PATH%dotnet.exe
REM if [%BUILDTOOLS_SOURCE%]==[] set BUILDTOOLS_SOURCE=https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json
REM set /P BUILDTOOLS_VERSION=< "%~dp0BuildToolsVersion.txt"
REM set BUILD_TOOLS_PATH=%PACKAGES_DIR%Microsoft.DotNet.BuildTools\%BUILDTOOLS_VERSION%\lib\
REM set PROJECT_JSON_PATH=%TOOLRUNTIME_DIR%\%BUILDTOOLS_VERSION%
REM set PROJECT_JSON_FILE=%PROJECT_JSON_PATH%\project.json
REM set PROJECT_JSON_CONTENTS={ "dependencies": { "Microsoft.DotNet.BuildTools": "%BUILDTOOLS_VERSION%" }, "frameworks": { "netcoreapp1.0": { } } }
REM set BUILD_TOOLS_SEMAPHORE=%PROJECT_JSON_PATH%\init-tools.completed

REM :: if force option is specified then clean the tool runtime and build tools package directory to force it to get recreated
REM if [%1]==[force] (
REM   if exist "%TOOLRUNTIME_DIR%" rmdir /S /Q "%TOOLRUNTIME_DIR%"
REM   if exist "%PACKAGES_DIR%Microsoft.DotNet.BuildTools" rmdir /S /Q "%PACKAGES_DIR%Microsoft.DotNet.BuildTools"
REM )

REM :: If sempahore exists do nothing
REM if exist "%BUILD_TOOLS_SEMAPHORE%" (
REM   echo Tools are already initialized.
REM   goto :EOF
REM )

REM if exist "%TOOLRUNTIME_DIR%" rmdir /S /Q "%TOOLRUNTIME_DIR%"

REM if NOT exist "%PROJECT_JSON_PATH%" mkdir "%PROJECT_JSON_PATH%"
REM echo %PROJECT_JSON_CONTENTS% > "%PROJECT_JSON_FILE%"
echo Running %0 > "%INIT_TOOLS_LOG%"

set /p DOTNET_VERSION=< "%~dp0DotnetCLIVersion.txt"
REM if exist "%DOTNET_CMD%" goto :afterdotnetrestore

echo Installing dotnet cli...
if NOT exist "%DOTNET_PATH%" mkdir "%DOTNET_PATH%"

set DOTNET_INSTALL_SCRIPT_URL=https://dot.net/v1/dotnet-install.ps1

REM The dotnet-install script respects the DOTNET_INSTALL_DIR environment variable
set DOTNET_INSTALL_DIR=%DOTNET_PATH%
set DOTNET_INSTALL_SCRIPT=%DOTNET_PATH%dotnet-install.ps1

powershell -NoProfile -ExecutionPolicy unrestricted -Command "Invoke-WebRequest $env:DOTNET_INSTALL_SCRIPT_URL -OutFile $env:DOTNET_INSTALL_SCRIPT" >> "%INIT_TOOLS_LOG%"

IF %ERRORLEVEL% NEQ 0 (
  echo ERROR: could not download dotnet-install script
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy unrestricted -Command "& $env:DOTNET_INSTALL_SCRIPT -Version $env:DOTNET_VERSION " >> "%INIT_TOOLS_LOG%"

IF %ERRORLEVEL% NEQ 0 (
  echo ERROR: could not download .NET Core SDK
  exit /b 1
)

REM set DOTNET_ZIP_NAME=dotnet-dev-win-x64.%DOTNET_VERSION%.zip
REM set DOTNET_REMOTE_PATH=https://dotnetcli.blob.core.windows.net/dotnet/Sdk/%DOTNET_VERSION%/%DOTNET_ZIP_NAME%
REM set DOTNET_LOCAL_PATH=%DOTNET_PATH%%DOTNET_ZIP_NAME%
REM echo Installing '%DOTNET_REMOTE_PATH%' to '%DOTNET_LOCAL_PATH%' >> "%INIT_TOOLS_LOG%"
REM powershell -NoProfile -ExecutionPolicy unrestricted -Command "$retryCount = 0; $success = $false; do { try { (New-Object Net.WebClient).DownloadFile('%DOTNET_REMOTE_PATH%', '%DOTNET_LOCAL_PATH%'); $success = $true; } catch { if ($retryCount -ge 6) { throw; } else { $retryCount++; Start-Sleep -Seconds (5 * $retryCount); } } } while ($success -eq $false); Add-Type -Assembly 'System.IO.Compression.FileSystem' -ErrorVariable AddTypeErrors; if ($AddTypeErrors.Count -eq 0) { [System.IO.Compression.ZipFile]::ExtractToDirectory('%DOTNET_LOCAL_PATH%', '%DOTNET_PATH%') } else { (New-Object -com shell.application).namespace('%DOTNET_PATH%').CopyHere((new-object -com shell.application).namespace('%DOTNET_LOCAL_PATH%').Items(),16) }" >> "%INIT_TOOLS_LOG%"
REM if NOT exist "%DOTNET_LOCAL_PATH%" (
REM   echo ERROR: Could not install dotnet cli correctly. See '%INIT_TOOLS_LOG%' for more details. 1>&2
REM   exit /b 1
REM )

:afterdotnetrestore

REM if exist "%BUILD_TOOLS_PATH%" goto :afterbuildtoolsrestore
REM echo Restoring BuildTools version %BUILDTOOLS_VERSION%...
REM echo Running: "%DOTNET_CMD%" restore "%PROJECT_JSON_FILE%" --no-cache --packages %PACKAGES_DIR% --source "%BUILDTOOLS_SOURCE%" >> "%INIT_TOOLS_LOG%"
REM call "%DOTNET_CMD%" restore "%PROJECT_JSON_FILE%" --no-cache --packages %PACKAGES_DIR% --source "%BUILDTOOLS_SOURCE%" >> "%INIT_TOOLS_LOG%"
REM if NOT exist "%BUILD_TOOLS_PATH%init-tools.cmd" (
REM   echo ERROR: Could not restore build tools correctly. See '%INIT_TOOLS_LOG%' for more details. 1>&2
REM   exit /b 1
REM )

:afterbuildtoolsrestore

REM echo Initializing BuildTools...
REM echo Running: "%BUILD_TOOLS_PATH%init-tools.cmd" "%~dp0" "%DOTNET_CMD%" "%TOOLRUNTIME_DIR%" >> "%INIT_TOOLS_LOG%"
REM call "%BUILD_TOOLS_PATH%init-tools.cmd" "%~dp0" "%DOTNET_CMD%" "%TOOLRUNTIME_DIR%" >> "%INIT_TOOLS_LOG%"
REM set INIT_TOOLS_ERRORLEVEL=%ERRORLEVEL%
REM if not [%INIT_TOOLS_ERRORLEVEL%]==[0] (
REM   echo ERROR: An error occured when trying to initialize the tools. Please check '%INIT_TOOLS_LOG%' for more details. 1>&2
REM   exit /b %INIT_TOOLS_ERRORLEVEL%
REM )

:: Create sempahore file
echo Done initializing tools.
REM echo Init-Tools.cmd completed for BuildTools Version: %BUILDTOOLS_VERSION% > "%BUILD_TOOLS_SEMAPHORE%"

:: Preserve original build number so the version-disambiguating logic in CreateNuGetPackages.proj
:: can access the revision (unique build number today).
if defined BUILD_BUILDNUMBER (
  echo ##vso[task.setvariable variable=MSBUILD_VSTS_ORIGINALBUILDNUMBER;]%BUILD_BUILDNUMBER%
)

exit /b 0