[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $hostType,
  [string] $configuration = "Debug",
  [switch] $prepareMachine,
  [bool] $buildStage1 = $True,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

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
  # Jenkins does not allow taskkill
  if (-not $ci) {
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
}

$RepoRoot = Join-Path $PSScriptRoot "..\"
$RepoRoot = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd($([System.IO.Path]::DirectorySeparatorChar));

$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$ArtifactsBinDir = Join-Path $ArtifactsDir "bin"
$LogDir = Join-Path $ArtifactsDir "log\$configuration"
$TempDir = Join-Path $ArtifactsDir "tmp\$configuration"
$VersionsProps = Join-Path $PSScriptRoot "Versions.props"

# $log = -not $nolog
# $restore = -not $norestore
# $runTests = (-not $skiptests) -or $test

if ($hostType -eq '')
{
  $hostType = 'full'
}

# TODO: If host type is full, either make sure we're running in a developer command prompt, or attempt to locate VS, or fail

$msbuildHost = $null
$msbuildToUse = "msbuild"

try {

  KillProcessesFromRepo

  if ($buildStage1)
  {
    & $PSScriptRoot\Common\Build.ps1 -restore -build /p:CreateBootstrap=true @properties
  }

  $bootstrapRoot = Join-Path $ArtifactsDir "bin\bootstrap"

  if ($hostType -eq 'full')
  {
    $buildToolPath = Join-Path $bootstrapRoot "net472\MSBuild\Current\Bin\MSBuild.exe"
    $buildToolCommand = "";

    if ($configuration -eq "Debug-MONO" -or $configuration -eq "Release-MONO")
    {
      # Copy MSBuild.dll to MSBuild.exe so we can run it without a host
      $sourceDll = Join-Path $bootstrapRoot "net472\MSBuild\Current\Bin\MSBuild.dll"
      Copy-Item -Path $sourceDll -Destination $msbuildToUse
    }
  }
  else
  {
    # we need to do this to guarantee we have/know where dotnet.exe is installed
    $dotnetToolPath = InitializeDotNetCli $true
    $buildToolPath = Join-Path $dotnetToolPath "dotnet.exe"
    $buildToolCommand = Join-Path $bootstrapRoot "netcoreapp2.1\MSBuild\MSBuild.dll"
  }

  # Use separate artifacts folder for stage 2
  $env:ArtifactsDir = Join-Path $ArtifactsDir "2\"

  $buildTool = @{ Path = $buildToolPath; Command = $buildToolCommand }
  $global:_BuildTool = $buildTool

  # When using bootstrapped MSBuild:
  # - Turn off node reuse (so that bootstrapped MSBuild processes don't stay running and lock files)
  # - Do run tests
  # - Don't try to create a bootstrap deployment
  & $PSScriptRoot\Common\Build.ps1 -restore -build -test /p:CreateBootstrap=false /nr:false @properties

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