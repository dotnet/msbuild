@echo off
setlocal

set MSBUILD_ROOT=%~dp0

REM Copy .NET 4.6 reference assemblies to the same folder as the MSBuild reference assemblies so ApiCompat can resolve their framework references

mkdir "%MSBUILD_ROOT%ref"

RoboCopy "C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6" "%MSBUILD_ROOT%ref"
RoboCopy "C:\Program Files (x86)\Reference Assemblies\Microsoft\MSBuild\v14.0" "%MSBUILD_ROOT%ref"

\\fxcore\tools\bin\ApiCompat.exe "%MSBUILD_ROOT%\bin\Windows_NT\Debug-NetCore\Microsoft.Build.Framework.dll,%MSBUILD_ROOT%\bin\Windows_NT\Debug-NetCore\Microsoft.Build.Utilities.Core.dll" -implDirs:"%MSBUILD_ROOT%ref" -contractDepends:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6,C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6\Facades" -out:Differences.txt

endlocal