# Sets up the shell session to use the bootstrap version of MSBuild
# Usage: . ./scripts/Use-BootstrapMSBuild.ps1

$repoRoot = Split-Path -Parent $PSScriptRoot
$bootstrapPath = Join-Path $repoRoot 'artifacts/bin/bootstrap/core/'

# Prepend bootstrap path to PATH
$env:PATH = "$bootstrapPath;$env:PATH"

# Set DOTNET_ROOT to bootstrap path
$env:DOTNET_ROOT = $bootstrapPath

# Set BuildWithNetFrameworkHostedCompiler to false
$env:BuildWithNetFrameworkHostedCompiler = 'false'

# Set DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT to true
$env:DOTNET_SYSTEM_NET_SECURITY_NOREVOCATIONCHECKBYDEFAULT = 'true'

# Set PowerShell window title
$host.UI.RawUI.WindowTitle = 'MSBuild dogfood'

Write-Host "Bootstrap MSBuild environment configured."
