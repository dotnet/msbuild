@if "%_echo%" neq "on" echo off
setlocal

if not defined VisualStudioVersion (
  if defined VS140COMNTOOLS (
    call "%VS140COMNTOOLS%\VsDevCmd.bat"
    goto :Run
  )
  echo Error: Visual Studio 2015 required.
  echo        Please see https://github.com/dotnet/cli/blob/rel/1.0.0/Documentation/developer-guide.md for build instructions.
  exit /b 1
)

:Run
powershell -NoProfile -NoLogo -Command "%~dp0build.cmd -NoBuild; exit $LastExitCode;"
set _toolRuntime=%~dp0build_tools
set _dotnet=%_toolRuntime%\dotnetcli\dotnet.exe

echo Running: %_dotnet% %_toolRuntime%\run.exe %*
call %_dotnet% %_toolRuntime%\run.exe %~dp0config.json %*
if NOT [%ERRORLEVEL%]==[0] (
  exit /b 1
)

exit /b 0