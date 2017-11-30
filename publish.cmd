SET PATH=%localappdata%\Microsoft\dotnet;%PATH%

call "%~dp0\build\EnsureWebSdkEnv.cmd"

msbuild "%WebSdkRoot%\publish\Publish.csproj" /t:Restore

msbuild "%WebSdkBuild%\publish.proj" /p:Configuration=%BuildConfiguration% %*