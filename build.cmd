@ECHO ON

PowerShell -NoProfile -NoLogo -ExecutionPolicy unrestricted -Command "[System.Threading.Thread]::CurrentThread.CurrentCulture = ''; [System.Threading.Thread]::CurrentThread.CurrentUICulture = '';& '%~dp0dotnet-install.ps1' %*; exit $LASTEXITCODE" 
SET PATH=%localappdata%\Microsoft\dotnet;%PATH%;
dotnet restore3 src\Microsoft.DotNetCore.Publish.Tasks\Microsoft.DotNetCore.Publish.Tasks.csproj
tools\build.cmd
