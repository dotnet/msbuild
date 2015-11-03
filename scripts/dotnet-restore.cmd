@Echo OFF
SETLOCAL
SET ERRORLEVEL=

"%~dp0dnx\dnx" "%~dp0dnx\lib\Microsoft.Dnx.Tooling\Microsoft.Dnx.Tooling.dll" restore %*

exit /b %ERRORLEVEL%
ENDLOCAL
