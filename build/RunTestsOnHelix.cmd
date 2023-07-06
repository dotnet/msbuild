
REM make NuGet network operations more robust
set NUGET_ENABLE_EXPERIMENTAL_HTTP_RETRY=true
set NUGET_EXPERIMENTAL_MAX_NETWORK_TRY_COUNT=6
set NUGET_EXPERIMENTAL_NETWORK_RETRY_DELAY_MILLISECONDS=1000

set MicrosoftNETBuildExtensionsTargets=%HELIX_CORRELATION_PAYLOAD%\ex\msbuildExtensions\Microsoft\Microsoft.NET.Build.Extensions\Microsoft.NET.Build.Extensions.targets
set DOTNET_ROOT=%HELIX_CORRELATION_PAYLOAD%\d
set PATH=%DOTNET_ROOT%;%PATH%
set DOTNET_MULTILEVEL_LOOKUP=0
set TestFullMSBuild=%1

set TestExecutionDirectory=%CD%\testExecutionDirectory
mkdir %TestExecutionDirectory%

REM Use powershell to call partical Arcade logic to get full framework msbuild path and assign it
if "%TestFullMSBuild%"=="true" (
    FOR /F "tokens=*" %%g IN ('PowerShell -ExecutionPolicy ByPass -File "%HELIX_CORRELATION_PAYLOAD%\t\eng\print-full-msbuild-path.ps1"') do (SET DOTNET_SDK_TEST_MSBUILD_PATH=%%g)
)

REM Use powershell to run GetRandomFileName
FOR /F "tokens=*" %%g IN ('PowerShell -ExecutionPolicy ByPass [System.IO.Path]::GetRandomFileName^(^)') do (SET RandomDirectoryName=%%g)
set TestExecutionDirectory=%TEMP%\dotnetSdkTests\%RandomDirectoryName%
set DOTNET_CLI_HOME=%TestExecutionDirectory%\.dotnet
mkdir %TestExecutionDirectory%
robocopy %HELIX_CORRELATION_PAYLOAD%\t\TestExecutionDirectoryFiles %TestExecutionDirectory% /s

set DOTNET_SDK_TEST_EXECUTION_DIRECTORY=%TestExecutionDirectory%
set DOTNET_SDK_TEST_MSBUILDSDKRESOLVER_FOLDER=%HELIX_CORRELATION_PAYLOAD%\r
set DOTNET_SDK_TEST_ASSETS_DIRECTORY=%TestExecutionDirectory%\assets

REM call dotnet new so the first run message doesn't interfere with the first test
dotnet new --debug:ephemeral-hive

REM We downloaded a special zip of files to the .nuget folder so add that as a source
dotnet nuget list source --configfile %TestExecutionDirectory%\nuget.config
PowerShell -ExecutionPolicy ByPass "dotnet nuget locals all -l | ForEach-Object { $_.Split(' ')[1]} | Where-Object{$_ -like '*cache'} | Get-ChildItem -Recurse -File -Filter '*.dat' | Measure"
dotnet nuget add source %DOTNET_ROOT%\.nuget --configfile %TestExecutionDirectory%\nuget.config

dotnet nuget remove source dotnet6-transport --configfile %TestExecutionDirectory%\nuget.config
dotnet nuget remove source dotnet6-internal-transport --configfile %TestExecutionDirectory%\nuget.config
dotnet nuget remove source dotnet7-transport --configfile %TestExecutionDirectory%\nuget.config
dotnet nuget remove source dotnet7-internal-transport --configfile %TestExecutionDirectory%\nuget.config
dotnet nuget remove source richnav --configfile %TestExecutionDirectory%\nuget.config
dotnet nuget remove source vs-impl --configfile %TestExecutionDirectory%\nuget.config
dotnet nuget remove source dotnet-libraries-transport --configfile %TestExecutionDirectory%\nuget.config
dotnet nuget remove source dotnet-tools-transport --configfile %TestExecutionDirectory%\nuget.config
dotnet nuget remove source dotnet-libraries --configfile %TestExecutionDirectory%\nuget.config
dotnet nuget remove source dotnet-eng --configfile %TestExecutionDirectory%\nuget.config
dotnet nuget list source --configfile %TestExecutionDirectory%\nuget.config

robocopy %HELIX_CORRELATION_PAYLOAD%\t\TestExecutionDirectoryFiles\ .\ testAsset.props
set TestPackagesRoot=%CD%\assets\testpackages\
dotnet build assets\testpackages\Microsoft.NET.TestPackages.csproj /t:Build -p:VersionPropsIsImported=false
robocopy .\assets\testpackages\testpackages %TestExecutionDirectory%\TestPackages /s