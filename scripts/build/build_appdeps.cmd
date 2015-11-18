@echo off

REM This file encapsulates the temporary steps to build the dotnet-compile-native command successfully
REM The AppDepSDK package is a temporary artifact until we have CoreRT assemblies published to Nuget

set __ScriptDir=%~dp0
set __RepoRoot=%__ScriptDir%\..\..
set __AppDepsProjectDir=%__RepoRoot%\src\Microsoft.DotNet.Tools.Compiler.Native\appdep

REM Get absolute path
pushd %1
set __OutputPath=%CD%\bin
popd


rmdir /S /Q %AppDepsProjectDir%\packages

pushd %__AppDepsProjectDir%
dotnet restore --packages %AppDepsProjectDir%\packages
set __AppDepSDK=%AppDepsProjectDir%\packages\toolchain*\
popd

mkdir %__OutputPath%\appdepsdk
cd %__AppDepSDK%
FOR /D %%a IN (*) DO (
  CD %%a
  GOTO :Copy
)

:Copy
xcopy /S/E/H/Y * %__OutputPath%\appdepsdk