SET PATH=%localappdata%\Microsoft\dotnet;%PATH%

call "%~dp0\build\EnsureWebSdkEnv.cmd"

msbuild "%WebSdkBuild%\PublishPackages.csproj" /t:Restore
msbuild "%WebSdkBuild%\publish.proj" /p:Configuration=%BuildConfiguration% %*