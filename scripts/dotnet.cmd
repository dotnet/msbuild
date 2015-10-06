@Echo OFF
SETLOCAL
SET ERRORLEVEL=

dnx %DOTNET_OPTIONS% -p %~dp0..\src\Microsoft.DotNet.Cli run %*

exit /b %ERRORLEVEL%
ENDLOCAL