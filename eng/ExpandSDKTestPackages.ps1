$systemTestPackages = "$Env:DOTNET_ROOT\.nuget\SDKTestPackages.System.zip";
$runtimeTestpackages = "$Env:DOTNET_ROOT\.nuget\SDKTestPackages.Runtime.zip";
if (-not(Test-Path -Path $systemTestPackages) -or -not(Test-Path -Path $runtimeTestpackages) )
{
	"Archive file of test packages not found at $Env:DOTNET_ROOT\.nuget";
	return;
}
"Expanding archive of sdk test packages"
Expand-Archive -Path $systemTestPackages -DestinationPath $Env:DOTNET_ROOT\.nuget
Expand-Archive -Path $runtimeTestpackages -DestinationPath $Env:DOTNET_ROOT\.nuget




