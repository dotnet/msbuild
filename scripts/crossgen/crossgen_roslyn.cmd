@echo off

REM Get absolute path
pushd %1
set BIN_DIR=%CD%
popd

REM Replace with a robust method for finding the right crossgen.exe
set CROSSGEN_UTIL=%UserProfile%\.dnx\packages\runtime.win7-x64.Microsoft.NETCore.Runtime.CoreCLR\1.0.1-beta-23428\tools\crossgen.exe

REM Crossgen currently requires itself to be next to mscorlib
copy %CROSSGEN_UTIL% /Y %BIN_DIR% > nul

pushd %BIN_DIR%

REM It must also be called mscorlib, not mscorlib.ni
if exist mscorlib.ni.dll (
    copy /Y mscorlib.ni.dll mscorlib.dll > nul
)

crossgen /nologo /ReadyToRun /Platform_Assemblies_Paths %BIN_DIR% System.Collections.Immutable.dll
if not %errorlevel% EQU 0 goto fail

crossgen /nologo /ReadyToRun /Platform_Assemblies_Paths %BIN_DIR% System.Reflection.Metadata.dll
if not %errorlevel% EQU 0 goto fail

crossgen /nologo /ReadyToRun /Platform_Assemblies_Paths %BIN_DIR% Microsoft.CodeAnalysis.dll
if not %errorlevel% EQU 0 goto fail

crossgen /nologo /ReadyToRun /Platform_Assemblies_Paths %BIN_DIR% Microsoft.CodeAnalysis.CSharp.dll
if not %errorlevel% EQU 0 goto fail

crossgen /nologo /ReadyToRun /Platform_Assemblies_Paths %BIN_DIR% Microsoft.CodeAnalysis.VisualBasic.dll
if not %errorlevel% EQU 0 goto fail

crossgen /nologo /ReadyToRun /Platform_Assemblies_Paths %BIN_DIR% csc.exe
if not %errorlevel% EQU 0 goto fail

crossgen /nologo /ReadyToRun /Platform_Assemblies_Paths %BIN_DIR% vbc.exe
if not %errorlevel% EQU 0 goto fail

popd
goto end

:fail
popd
echo Crossgen failed...
exit /B 1

:end
