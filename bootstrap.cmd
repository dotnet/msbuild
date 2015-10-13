REM Build 'dotnet' using a version of 'dotnet' hosted on the DNX
REM The output of this is independent of DNX

call "%~dp0scripts\dotnet" publish --framework dnxcore50 --runtime win7-x64 --output "%~dp0artifacts\published" "%~dp0src\Microsoft.DotNet.Cli"
call "%~dp0scripts\dotnet" publish --framework dnxcore50 --runtime win7-x64 --output "%~dp0artifacts\published" "%~dp0src\Microsoft.DotNet.Tools.Compiler"
call "%~dp0scripts\dotnet" publish --framework dnxcore50 --runtime win7-x64 --output "%~dp0artifacts\published" "%~dp0src\Microsoft.DotNet.Tools.Publish"