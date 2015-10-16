@Echo OFF
SETLOCAL
SET ERRORLEVEL=

REM makes testing easier for now
set PATH=%ProgramFiles(x86)%\MSBuild\14.0\Bin;%PATH%;%~dp0

dnx %DOTNET_OPTIONS% -p %~dp0..\..\src\Microsoft.DotNet.Tools.Compiler run %*

exit /b %ERRORLEVEL%
ENDLOCAL
