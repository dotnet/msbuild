REM Turn echo off off so we can echo with echo and the echoing
REM (But seriously, this script has weird hangs and crashes sometimes so we want to know exactly which commands are failing)
REM @echo off

REM Copyright (c) .NET Foundation and contributors. All rights reserved.
REM Licensed under the MIT license. See LICENSE file in the project root for full license information.

if %SKIP_CROSSGEN% EQU 0 goto skip

echo Crossgenning Roslyn compiler ...

REM Get absolute path
pushd %1
set BIN_DIR=%CD%\bin
popd

REM Replace with a robust method for finding the right crossgen.exe
set CROSSGEN_UTIL=%NUGET_PACKAGES%\runtime.win7-x64.Microsoft.NETCore.Runtime.CoreCLR\1.0.1-rc2-23811\tools\crossgen.exe

REM Crossgen currently requires itself to be next to mscorlib
copy %CROSSGEN_UTIL% /Y %BIN_DIR% > nul

pushd %BIN_DIR%

REM It must also be called mscorlib, not mscorlib.ni
if exist mscorlib.ni.dll (
    copy /Y mscorlib.ni.dll mscorlib.dll > nul
)

set READYTORUN=

crossgen /nologo %READYTORUN% /Platform_Assemblies_Paths %BIN_DIR% System.Collections.Immutable.dll >nul 2>nul
if not %errorlevel% EQU 0 goto fail

crossgen /nologo %READYTORUN% /Platform_Assemblies_Paths %BIN_DIR% System.Reflection.Metadata.dll >nul 2>nul
if not %errorlevel% EQU 0 goto fail

crossgen /nologo %READYTORUN% /Platform_Assemblies_Paths %BIN_DIR% Microsoft.CodeAnalysis.dll >nul 2>nul
if not %errorlevel% EQU 0 goto fail

crossgen /nologo %READYTORUN% /Platform_Assemblies_Paths %BIN_DIR% Microsoft.CodeAnalysis.CSharp.dll >nul 2>nul
if not %errorlevel% EQU 0 goto fail

echo Crossgenning Microsoft.CodeAnalysis.VisualBasic
crossgen /nologo %READYTORUN% /Platform_Assemblies_Paths %BIN_DIR% Microsoft.CodeAnalysis.VisualBasic.dll >nul 2>nul
if not %errorlevel% EQU 0 goto fail

echo Crossgenning csc
crossgen /nologo %READYTORUN% /Platform_Assemblies_Paths %BIN_DIR% csc.dll >nul 2>nul
if not %errorlevel% EQU 0 goto fail

echo Crossgenning vbc
crossgen /nologo %READYTORUN% /Platform_Assemblies_Paths %BIN_DIR% vbc.dll >nul 2>nul
if not %errorlevel% EQU 0 goto fail

popd

echo CrossGen Roslyn Finished

goto end

:fail
popd
echo Crossgen failed...
exit /B 1

:skip
echo Skipping Crossgen
goto end

:end
