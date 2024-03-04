[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $msbuildEngine,
  [string] $configuration = "Debug",
  [switch] $prepareMachine,
  [bool] $buildStage1 = $True,
  [bool] $onlyDocChanged = 0,
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
    & $PSScriptRoot\Common\Build.ps1 -restore -build -ci -msbuildEngine $msbuildEngine /p:CreateBootstrap=true @properties
  }

  KillProcessesFromRepo

  $bootstrapRoot = Join-Path $Stage1BinDir "bootstrap"

  # we need to do this to guarantee we have/know where dotnet.exe is installed
  $dotnetToolPath = InitializeDotNetCli $true
  $dotnetExePath = Join-Path $dotnetToolPath "dotnet.exe"

  if ($msbuildEngine -eq 'vs')
  {
    $buildToolPath = Join-Path $bootstrapRoot "net472\MSBuild\Current\Bin\MSBuild.exe"
    $buildToolCommand = "";
    $buildToolFramework = "net472"
  }
  else
  {
    $buildToolPath = $dotnetExePath
    $buildToolCommand = Join-Path $bootstrapRoot "net8.0\MSBuild\MSBuild.dll"
    $buildToolFramework = "net8.0"
  }

  # Use separate artifacts folder for stage 2
  # $env:ArtifactsDir = Join-Path $ArtifactsDir "2\"

  & $dotnetExePath build-server shutdown

  if ($buildStage1)
  {
    if (Test-Path $Stage1Dir)
    {
      Remove-Item -Force -Recurse $Stage1Dir
    }

    Move-Item -Path $ArtifactsDir -Destination $Stage1Dir -Force
  }

  $buildTool = @{ Path = $buildToolPath; Command = $buildToolCommand; Tool = $msbuildEngine; Framework = $buildToolFramework }
  $global:_BuildTool = $buildTool

  # Ensure that debug bits fail fast, rather than hanging waiting for a debugger attach.
  $env:MSBUILDDONOTLAUNCHDEBUGGER="true"

  # Opt into performance logging. https://github.com/dotnet/msbuild/issues/5900
  $env:DOTNET_PERFLOG_DIR=$PerfLogDir

  # When using bootstrapped MSBuild:
  # - Turn off node reuse (so that bootstrapped MSBuild processes don't stay running and lock files)
  # - Do run tests
  # - Don't try to create a bootstrap deployment
  if ($onlyDocChanged) {
    & $PSScriptRoot\Common\Build.ps1 -restore -build -ci /p:CreateBootstrap=false /nr:false @properties
  }
  else {
    & $PSScriptRoot\Common\Build.ps1 -restore -build -test -ci /p:CreateBootstrap=false /nr:false @properties
  }

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
