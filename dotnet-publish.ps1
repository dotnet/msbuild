$OutputName = Split-Path -Leaf (Get-Location)
$OutputPath = Join-Path (Join-Path (Get-Location) "bin") "publish"

& $PSScriptRoot\dotnet-compile.ps1

if(Test-Path $OutputPath) {
    del -Recurse -Force $OutputPath
}
mkdir $OutputPath | Out-Null
$OutputPath = (Convert-Path $OutputPath)

# Resolve runtime and native dependencies
Write-Host "Resolving dependencies..."
$refs = dnx -p "$PSScriptRoot\src\DotNet.Tools.DependencyResolver" run --packages "$env:USERPROFILE\.dnx\packages" --target "DNXCore,Version=v5.0/win7-x64" --assets runtime --assets native

# Copy everything to one directory
$refs | foreach {
    Write-Host "Publishing $_ ..."
    cp $_ $OutputPath   
}

$ProjectBinary = (Join-Path (Get-Location) "bin\$OutputName.dll")
Write-Host "Publishing $ProjectBinary ..."
cp $ProjectBinary $OutputPath

# CoreConsole should have come along for the ride
$CoreConsolePath = Join-Path $OutputPath "CoreConsole.exe"
if(!(Test-Path $CoreConsolePath)) {
    throw "Unable to locate CoreConsole.exe. You must have a dependency on Microsoft.NETCore.ConsoleHost (for now ;))"
}
mv $CoreConsolePath (Join-Path $OutputPath "$OutputName.exe")