$OutputName = Split-Path -Leaf (Get-Location)

& $PSScriptRoot\dotnet-compile.ps1

# Still need to use DNX, but it just boots CoreCLR, no runtime involved.
dnx run bin\$OutputName.exe