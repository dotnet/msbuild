@echo off

setlocal

REM This trick gets the absolute path from a relative path
pushd %~dp0..
set REPOROOT=%CD%
popd

set RID=win7-x64
set TFM=dnxcore50
set DNX_DIR=%REPOROOT%\artifacts\%RID%\dnx
set STAGE1_DIR=%REPOROOT%\artifacts\%RID%\stage1
set STAGE2_DIR=%REPOROOT%\artifacts\%RID%\stage2
set HOST_DIR=%REPOROOT%\artifacts\%RID%\corehost
set START_PATH=%PATH%

if "%CONFIGURATION%" equ "" set CONFIGURATION=Debug

call %~dp0dnvm2.cmd upgrade -a dotnet_stage0
if errorlevel 1 goto fail

REM Gross! But CMD has no other way to do this :(
where dotnet > "%temp%\dotnet-cli-build-temp.tmp"
set /P DOTNET_PATH= < "%temp%\dotnet-cli-build-temp.tmp"
pushd "%DOTNET_PATH%\.."
set STAGE0_DIR=%CD%
set DNX_ROOT=%STAGE0_DIR%\dnx
popd

echo Building corehost
set CMAKE_OUTPUT=%~dp0..\src\corehost\cmake
if not exist "%CMAKE_OUTPUT%" mkdir "%CMAKE_OUTPUT%"
pushd "%CMAKE_OUTPUT%"
cmake .. -G "Visual Studio 14 2015 Win64"
if %errorlevel% neq 0 exit /b %errorlevel%
"%ProgramFiles(x86)%\MSBuild\14.0\Bin\MSBuild.exe" ALL_BUILD.vcxproj /p:Configuration="%CONFIGURATION%"

if exist "%HOST_DIR%" rd /s /q "%HOST_DIR%"
mkdir "%HOST_DIR%"
copy "%CONFIGURATION%\*" "%HOST_DIR%"

popd

if exist "%STAGE1_DIR%" rd /s /q "%STAGE1_DIR%"

echo Running 'dotnet restore' to restore packages
call dotnet restore "%REPOROOT%" --runtime "osx.10.10-x64" --runtime "ubuntu.14.04-x64" --runtime "win7-x64"
if errorlevel 1 goto fail

echo Building basic dotnet tools using older dotnet SDK version

echo Building stage1 dotnet.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE1_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Cli"
if errorlevel 1 goto fail

echo Building stage1 dotnet-compile.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE1_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Compiler"
if errorlevel 1 goto fail

echo Building stage1 dotnet-compile-csc.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE1_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Compiler.Csc"
if errorlevel 1 goto fail

echo Building stage1 dotnet-repl.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE1_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Repl"
if errorlevel 1 goto fail

echo Building stage1 dotnet-repl-csi.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE1_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Repl.Csi"
if errorlevel 1 goto fail

echo Building stage1 dotnet-publish.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE1_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Publish"
if errorlevel 1 goto fail

echo Building stage1 resgen.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE1_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Resgen"
if errorlevel 1 goto fail

echo Building stage1 dotnet-run.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE1_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Run"
if errorlevel 1 goto fail

REM deploy corehost.exe to the output
copy "%HOST_DIR%\corehost.exe" "%STAGE1_DIR%"
if errorlevel 1 goto fail

if exist "%HOST_DIR%\corehost.pdb" copy "%HOST_DIR%\corehost.pdb" "%STAGE1_DIR%"
if errorlevel 1 goto fail

echo Re-building dotnet tools with the bootstrapped version
REM This should move into a proper build script of some kind once we are bootstrapped
set PATH=%STAGE1_DIR%;%START_PATH%

if exist %STAGE2_DIR% rd /s /q %STAGE2_DIR%

echo Building stage2 dotnet.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE2_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Cli"
if errorlevel 1 goto fail

echo Building stage2 dotnet-compile.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE2_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Compiler"
if errorlevel 1 goto fail

echo Building stage2 dotnet-compile-csc.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE2_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Compiler.Csc"
if errorlevel 1 goto fail

echo Building stage2 dotnet-repl.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE2_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Repl"
if errorlevel 1 goto fail

echo Building stage2 dotnet-repl-csi.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE2_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Repl.Csi"
if errorlevel 1 goto fail

echo Building stage2 dotnet-publish.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE2_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Publish"
if errorlevel 1 goto fail

echo Building stage2 resgen.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE2_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Resgen"
if errorlevel 1 goto fail

echo Building stage2 dotnet-run.exe ...
dotnet publish --framework "%TFM%" --runtime "%RID%" --output "%STAGE2_DIR%" --configuration "%CONFIGURATION%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Run"
if errorlevel 1 goto fail

REM deploy corehost.exe to the output
copy "%HOST_DIR%\corehost.exe" "%STAGE2_DIR%"
if errorlevel 1 goto fail

if exist "%HOST_DIR%\corehost.pdb" copy "%HOST_DIR%\corehost.pdb" "%STAGE2_DIR%"
if errorlevel 1 goto fail

echo Crossgening Roslyn compiler ...
call "%~dp0crossgen\crossgen_roslyn.cmd" "%STAGE2_DIR%"
if errorlevel 1 goto fail

REM Copy DNX in to stage2
xcopy /s /q "%DNX_ROOT%" "%STAGE2_DIR%\dnx\"

REM Copy the dotnet-restore script
copy "%~dp0dotnet-restore.cmd" "%STAGE2_DIR%\dotnet-restore.cmd"

REM Smoke-test the output
set PATH=%STAGE2_DIR%;%START_PATH%

del "%REPOROOT%\test\TestApp\project.lock.json"
dotnet restore "%REPOROOT%\test\TestApp" --quiet --runtime "osx.10.10-x64" --runtime "ubuntu.14.04-x64" --runtime "win7-x64"
dotnet compile "%REPOROOT%\test\TestApp" --output "%REPOROOT%\artifacts\%RID%\smoketest"

set CLRHOST_CLR_PATH=%STAGE2_DIR%
"%REPOROOT%\artifacts\%RID%\smoketest\TestApp" 2>nul >nul
if errorlevel 1 goto fail

REM Check that a compiler error is reported
dotnet compile "%REPOROOT%\test\compile\failing\SimpleCompilerError" --framework "%TFM%" >nul 2>nul
if %errorlevel% == 0 goto fail

echo Bootstrapped dotnet to %STAGE2_DIR%

goto end

:fail
echo Bootstrapping failed
exit /B 1

:end
exit /B 0
