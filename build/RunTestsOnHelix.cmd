set TestSubjectMSBuildSDKsPath=%HELIX_CORRELATION_PAYLOAD%\s
set DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR=%TestSubjectMSBuildSDKsPath%
set MicrosoftNETBuildExtensionsTargets=%TestSubjectMSBuildSDKsPath%\Microsoft.NET.Build.Extensions\msbuildExtensions\Microsoft\Microsoft.NET.Build.Extensions\Microsoft.NET.Build.Extensions.targets
set DOTNET_ROOT=%HELIX_CORRELATION_PAYLOAD%\d
set PATH=%DOTNET_ROOT%;%PATH%
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
mkdir %TestExecutionDirectory%
robocopy %HELIX_CORRELATION_PAYLOAD%\t\TestExecutionDirectoryFiles %TestExecutionDirectory%
