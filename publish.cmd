@ECHO OFF

PowerShell -NoProfile -NoLogo -ExecutionPolicy unrestricted -Command "[System.Threading.Thread]::CurrentThread.CurrentCulture = ''; [System.Threading.Thread]::CurrentThread.CurrentUICulture = '';& '%~dp0dotnet-install_2.0.ps1' %*; exit $LASTEXITCODE" 
SET PATH=%localappdata%\Microsoft\dotnet;%PATH%

call "%~dp0\build\EnsureWebSdkEnv.cmd"

msbuild "%WebSdkRoot%\publish\Publish.csproj" /t:Restore

msbuild "%WebSdkBuild%\publish.proj" /p:Configuration=%BuildConfiguration% /t:Publish /v:diag %*