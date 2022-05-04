$testpackages = "$Env:DOTNET_ROOT\.nuget\SDKTestPackages.System.zip";
if (-not(Test-Path -Path $testpackages))
{
	"Archive file of test packages not found at $Env:DOTNET_ROOT\.nuget";
	return;
}
"Expanding archive of sdk test packages"
Expand-Archive -Path $testpackages -DestinationPath $Env:DOTNET_ROOT\.nuget




