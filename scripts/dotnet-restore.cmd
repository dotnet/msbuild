@Echo OFF
SETLOCAL
SET ERRORLEVEL=

"%~dp0dnx\dnx" "%~dp0dnx\lib\Microsoft.Dnx.Tooling\Microsoft.Dnx.Tooling.dll" restore --runtime "osx.10.10-x64" --runtime "ubuntu.14.04-x64" %*

exit /b %ERRORLEVEL%
ENDLOCAL
