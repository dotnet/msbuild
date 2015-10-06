$OutputName = Split-Path -Leaf (Get-Location)
$OutputPath = Join-Path (Get-Location) "bin"

if(!(Test-Path $OutputPath)) {
    mkdir $OutputPath | Out-Null
}
$OutputPath = (Convert-Path $OutputPath)


# Resolve compilation dependencies
Write-Host "Resolving dependencies..."
$refs = dnx -p "$PSScriptRoot\src\DotNet.Tools.DependencyResolver" run --packages "$env:USERPROFILE\.dnx\packages" --target "DNXCore,Version=v5.0" --assets compile

# Resolve source files
Write-Host "Finding source files..."
$srcs = dnx -p "$PSScriptRoot\src\DotNet.Tools.SourceResolver" run

# Build csc response file
$resp = @($refs | foreach { "/r:$_" })
$resp += @($srcs | foreach { $_ })
$resp += @(
    "/out:$OutputPath\$OutputName.dll"
    "/nostdlib"
)

Write-Host "Compiling..."
$resp > "$OutputPath\csc.rsp"
csc "@$OutputPath\csc.rsp"

Write-Host " $OutputName -> $OutputPath\$OutputName.dll"