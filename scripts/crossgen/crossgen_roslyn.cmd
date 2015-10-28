@echo off

REM Get absolute path
pushd %1
set BIN_DIR=%CD%
popd

REM Replace with a robust method for finding the right crossgen.exe
set CROSSGEN_UTIL=%UserProfile%\.dnx\packages\runtime.win7-x64.Microsoft.NETCore.Runtime.CoreCLR\1.0.1-beta-23419\tools\crossgen.exe

pushd %BIN_DIR%

%CROSSGEN_UTIL% /nologo /Platform_Assemblies_Paths %BIN_DIR% System.Collections.Immutable.dll
if errorlevel 1 goto fail

%CROSSGEN_UTIL% /nologo /Platform_Assemblies_Paths %BIN_DIR% System.Reflection.Metadata.dll
if errorlevel 1 goto fail

%CROSSGEN_UTIL% /nologo /Platform_Assemblies_Paths %BIN_DIR% Microsoft.CodeAnalysis.dll
if errorlevel 1 goto fail

%CROSSGEN_UTIL% /nologo /Platform_Assemblies_Paths %BIN_DIR% Microsoft.CodeAnalysis.CSharp.dll
if errorlevel 1 goto fail

%CROSSGEN_UTIL% /nologo /Platform_Assemblies_Paths %BIN_DIR% Microsoft.CodeAnalysis.VisualBasic.dll
if errorlevel 1 goto fail

%CROSSGEN_UTIL% /nologo /Platform_Assemblies_Paths %BIN_DIR% csc.exe
if errorlevel 1 goto fail

%CROSSGEN_UTIL% /nologo /Platform_Assemblies_Paths %BIN_DIR% vbc.exe
if errorlevel 1 goto fail

popd
goto end

:fail
popd
echo Crossgen failed...
exit /B 1

:end
