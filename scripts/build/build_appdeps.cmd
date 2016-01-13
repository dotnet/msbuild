REM TEMPORARILY disable @echo off to debug CI.
REM @echo off

REM This file encapsulates the temporary steps to build the dotnet-compile-native command successfully
REM The AppDepSDK package is a temporary artifact until we have CoreRT assemblies published to Nuget

set __ScriptDir=%~dp0
set __RepoRoot=%__ScriptDir%\..\..
set __AppDepsProjectDir=%__RepoRoot%\src\Microsoft.DotNet.Tools.Compiler.Native\appdep

REM Get absolute path
pushd %1
set __OutputPath=%CD%\bin
popd


pushd %__AppDepsProjectDir%
rmdir /S /Q packages
dotnet restore --packages %__AppDepsProjectDir%\packages
set __AppDepSDK=%__AppDepsProjectDir%\packages\toolchain*\
popd

mkdir %__OutputPath%\appdepsdk
cd %__AppDepSDK%
FOR /D %%a IN (*) DO (
  CD %%a
  TREE
  GOTO :Copy
)

:Copy
xcopy /S/E/H/Y * %__OutputPath%\appdepsdk
