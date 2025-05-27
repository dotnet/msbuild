[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $msbuildEngine,
  [string] $configuration = "Debug",
  [switch] $prepareMachine,
  [bool] $buildStage1 = $True,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

# Ensure that static state in tools is aware that this is
# a CI scenario
$ci = $true

. $PSScriptRoot\common\tools.ps1

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

# Opt into TLS 1.2, which is required for https://dot.net
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

function Stop-Processes() {
  Write-Host "Killing running build processes..."
  Get-Process -Name "msbuild" -ErrorAction SilentlyContinue | Stop-Process
  Get-Process -Name "vbcscompiler" -ErrorAction SilentlyContinue | Stop-Process
}

function KillProcessesFromRepo {
  # Kill compiler server and MSBuild node processes from bootstrapped MSBuild (otherwise a second build will fail to copy files in use)
  foreach ($process in Get-Process | Where-Object {'msbuild', 'dotnet', 'vbcscompiler' -contains $_.Name})
  {

    if ([string]::IsNullOrEmpty($process.Path))
    {
      Write-Host "Process $($process.Id) $($process.Name) does not have a Path. Skipping killing it."
      continue
    }

    if ($process.Path.StartsWith($RepoRoot, [StringComparison]::InvariantCultureIgnoreCase))
    {
      Write-Host "Killing $($process.Name) from $($process.Path)"
      taskkill /f /pid $process.Id
    }
  }
}

$RepoRoot = Join-Path $PSScriptRoot "..\"
$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd($([System.IO.Path]::DirectorySeparatorChar));

$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$Stage1Dir = Join-Path $RepoRoot "stage1"
$Stage1BinDir = Join-Path $Stage1Dir "bin"
$PerfLogDir = Join-Path $ArtifactsDir "log\$Configuration\PerformanceLogs"

if ($msbuildEngine -eq '')
{
  $msbuildEngine = 'vs'
}

$msbuildToUse = "msbuild"

try {
  KillProcessesFromRepo

  if ($buildStage1)
  {
    & $PSScriptRoot\Common\Build.ps1 -restore -build -test -ci -msbuildEngine $msbuildEngine /p:CreateBootstrap=true @properties
  }

  KillProcessesFromRepo

  exit $lastExitCode
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  exit 1
}
finally {
  Pop-Location
  if ($prepareMachine) {
    Stop-Processes
  }
}
