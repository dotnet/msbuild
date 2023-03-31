[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Debug",
  [string] $projects = "",
  [switch] $help,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$command
)

set-strictmode -version 2.0
$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$restore = $true

. $PSScriptRoot\common\tools.ps1

. $PSScriptRoot\configure-toolset.ps1

function Print-Usage() {
  Write-Host "Common settings:"
  Write-Host "  -configuration <value>  Build configuration Debug, Release"
  Write-Host "  -help                   Print help and exit"
  Write-Host ""
  Write-Host "Command line arguments not listed above are interpreted as a command to be run in the dogfood context."
  Write-Host "If no additional arguments are specified, then the value of the DOTNET_SDK_DOGFOOD_SHELL environment,"
  Write-Host "if it is set, will be used."
}

if ($help -or (($command -ne $null) -and ($command.Contains("/help") -or $command.Contains("/?")))) {
  Print-Usage
  exit 0
}

try {
  $toolsetBuildProj = InitializeToolset
  . $PSScriptroot\restore-toolset.ps1

  $env:SDK_REPO_ROOT = $RepoRoot

  $TestDotnetRoot = Join-Path $ArtifactsDir "bin\redist\$configuration\dotnet"

  $testDotnetVersion = (Get-Childitem -Directory "$TestDotnetRoot\sdk")[-1]
  $env:DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR = Join-Path $TestDotnetRoot "sdk\$testDotnetVersion\Sdks"
  $env:MicrosoftNETBuildExtensionsTargets = Join-Path $ArtifactsDir "bin\$configuration\Sdks\Microsoft.NET.Build.Extensions\msbuildExtensions\Microsoft\Microsoft.NET.Build.Extensions\Microsoft.NET.Build.Extensions.targets"

  $env:PATH = "$TestDotnetRoot;$env:Path"
  $env:DOTNET_ROOT = $TestDotnetRoot

  if ($command -eq $null -and $env:DOTNET_SDK_DOGFOOD_SHELL -ne $null) {
    $command = , $env:DOTNET_SDK_DOGFOOD_SHELL
  }

  if ($command -ne $null) {
    $Host.UI.RawUI.WindowTitle = "SDK Test ($RepoRoot) ($configuration)"
    & $command[0] $command[1..($command.Length-1)]
  }
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  ExitWithExitCode 1
}
