SET PATH=%localappdata%\Microsoft\dotnet;%PATH%

call "%~dp0\build\EnsureWebSdkEnv.cmd"
xcopy \\aspnetci\share\tools\websdk\WebDeploy\* "%WebSdkBuild%\WebDeploy\*" /y /C /e /s /f

msbuild "%WebSdkBuild%\BuildPackages.csproj" /t:Restore
msbuild "%WebSdkBuild%\build.proj" /p:Configuration=%BuildConfiguration% /t:Build %*
msbuild "%WebSdkBuild%\build.proj" /p:Configuration=%BuildConfiguration% /t:SignPackages %*
