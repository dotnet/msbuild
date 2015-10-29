@echo off

setlocal

REM Build 'dotnet' using a version of itself hosted on the DNX
REM The output of this is independent of DNX

REM This trick gets the absolute path from a relative path
pushd %~dp0..
set REPOROOT=%CD%
popd

set RID=win7-x64
set TFM=dnxcore50
set DNX_DIR=%REPOROOT%\artifacts\%RID%\dnx
set STAGE0_DIR=%REPOROOT%\artifacts\%RID%\stage0
set STAGE1_DIR=%REPOROOT%\artifacts\%RID%\stage1
set STAGE2_DIR=%REPOROOT%\artifacts\%RID%\stage2
set START_PATH=%PATH%

where dnvm >nul 2>nul
if %errorlevel% == 0 goto have_dnvm
echo DNVM must be installed to bootstrap dotnet
exit /B 1

:have_dnvm
if not exist %DNX_DIR% mkdir %DNX_DIR%
set DNX_HOME=%DNX_DIR%
set DNX_USER_HOME=%DNX_DIR%
set DNX_GLOBAL_HOME=%DNX_DIR%

echo Installing and use-ing the latest CoreCLR x64 DNX ...
call dnvm install -nonative -u latest -r coreclr -arch x64 -alias dotnet_bootstrap
pushd "%DNX_DIR%"
cd "runtimes\dnx-*"
set DNX_ROOT=%CD%\bin
popd
if errorlevel 1 goto fail

call dnvm use dotnet_bootstrap -r coreclr -arch x64
if errorlevel 1 goto fail

if exist %STAGE1_DIR% rd /s /q %STAGE1_DIR%

echo Running 'dnu restore' to restore packages for DNX-hosted projects
call dnu restore "%REPOROOT%"
if errorlevel 1 goto fail

echo Building basic dotnet tools using older dotnet SDK version

set DOTNET_HOME=%STAGE0_DIR%
set DOTNET_USER_HOME=%STAGE0_DIR%
set DOTNET_GLOBAL_HOME=%STAGE0_DIR%

call %~dp0dnvm2 upgrade
if errorlevel 1 goto fail

echo Building stage1 dotnet.exe ...
dotnet-publish --framework %TFM% --runtime %RID% --output "%STAGE1_DIR%" "%REPOROOT%\src\Microsoft.DotNet.Cli"
if errorlevel 1 goto fail

echo Building stage1 dotnet-compile.exe ...
dotnet-publish --framework %TFM% --runtime %RID% --output "%STAGE1_DIR%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Compiler"
if errorlevel 1 goto fail

echo Building stage1 dotnet-compile-csc.exe ...
dotnet-publish --framework %TFM% --runtime %RID% --output "%STAGE1_DIR%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Compiler.Csc"
if errorlevel 1 goto fail

echo Building stage1 dotnet-publish.exe ...
dotnet-publish --framework %TFM% --runtime %RID% --output "%STAGE1_DIR%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Publish"
if errorlevel 1 goto fail

echo Building stage1 dotnet-publish.exe ...
dotnet-publish --framework %TFM% --runtime %RID% --output "%STAGE1_DIR%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Resgen"
if errorlevel 1 goto fail

echo Re-building dotnet tools with the bootstrapped version
REM This should move into a proper build script of some kind once we are bootstrapped
set PATH=%STAGE1_DIR%;%START_PATH%

if exist %STAGE2_DIR% rd /s /q %STAGE2_DIR%

echo Building stage2 dotnet.exe ...
dotnet publish --framework %TFM% --runtime %RID% --output "%STAGE2_DIR%" "%REPOROOT%\src\Microsoft.DotNet.Cli"
if errorlevel 1 goto fail

echo Building stage2 dotnet-compile.exe ...
dotnet publish --framework %TFM% --runtime %RID% --output "%STAGE2_DIR%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Compiler"
if errorlevel 1 goto fail

echo Building stage2 dotnet-compile-csc.exe ...
dotnet publish --framework %TFM% --runtime %RID% --output "%STAGE2_DIR%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Compiler.Csc"
if errorlevel 1 goto fail

echo Building stage2 dotnet-publish.exe ...
dotnet publish --framework %TFM% --runtime %RID% --output "%STAGE2_DIR%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Publish"
if errorlevel 1 goto fail

echo Building stage2 dotnet-publish.exe ...
dotnet publish --framework %TFM% --runtime %RID% --output "%STAGE2_DIR%" "%REPOROOT%\src\Microsoft.DotNet.Tools.Resgen"
if errorlevel 1 goto fail

echo Crossgening Roslyn compiler ...
call %~dp0crossgen/crossgen_roslyn.cmd %STAGE2_DIR%
if errorlevel 1 goto fail

REM Copy DNX in to stage2
xcopy /s /q %DNX_ROOT% %STAGE2_DIR%\dnx\

REM Clean up some things we don't need
rd /s /q %STAGE2_DIR%\dnx\lib\Microsoft.Dnx.DesignTimeHost
rd /s /q %STAGE2_DIR%\dnx\lib\Microsoft.Dnx.Project
del %STAGE2_DIR%\dnx\dnu.cmd

REM Copy and CHMOD the dotnet-restore script
copy %~dp0dotnet-restore.cmd %STAGE2_DIR%\dotnet-restore.cmd

# Smoke-test the output
set PATH=%STAGE2_DIR%;%START_PATH%

del "%REPOROOT%\test\TestApp\project.lock.json"
dotnet restore "%REPOROOT%\test\TestApp" --runtime "%RID%"
dotnet publish "%REPOROOT%\test\TestApp" --framework "%TFM%" --runtime "%RID%" --output "%REPOROOT%\artifacts\%RID%\smoketest"

"%REPOROOT%/artifacts/%RID%/smoketest/TestApp"
if errorlevel 1 goto fail

REM Check that a compiler error is reported
dotnet compile "%REPOROOT%\test\compile\failing\SimpleCompilerError" --framework "%TFM%"
if %errorlevel% == 0 goto fail

echo Bootstrapped dotnet to %STAGE2_DIR%

goto end

:fail
echo Bootstrapping failed
exit /B 1

:end
exit /B 0
