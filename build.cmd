@ECHO OFF

PowerShell -NoProfile -NoLogo -ExecutionPolicy unrestricted -Command "[System.Threading.Thread]::CurrentThread.CurrentCulture = ''; [System.Threading.Thread]::CurrentThread.CurrentUICulture = '';& '%~dp0dotnet-install_1.0.ps1' %*; exit $LASTEXITCODE" 
PowerShell -NoProfile -NoLogo -ExecutionPolicy unrestricted -Command "[System.Threading.Thread]::CurrentThread.CurrentCulture = ''; [System.Threading.Thread]::CurrentThread.CurrentUICulture = '';& '%~dp0dotnet-install_2.0.ps1' %*; exit $LASTEXITCODE" 
SET PATH=%localappdata%\Microsoft\dotnet;%PATH%

call "%~dp0\build\EnsureWebSdkEnv.cmd"
xcopy \\aspnetci\share\tools\websdk\WebDeploy\* "%WebSdkBuild%\WebDeploy\*" /y /C /e /s /f

%DOTNET_INSTALL_DIR%\dotnet msbuild "%WebSdkBuild%\build.proj" /p:Configuration=%BuildConfiguration% /t:Build %*
