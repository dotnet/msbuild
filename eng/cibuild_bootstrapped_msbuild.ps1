[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $msbuildEngine,
  [string] $configuration = "Debug",
  [switch] $prepareMachine,
  [bool] $buildStage1 = $True,
  [bool] $onlyDocChanged = 0,
  [switch] $skipTests,
  [string] $stage2Properties = "",
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$ArtifactsDir = Join-Path $RepoRoot 'artifacts'
$Stage1Dir = Join-Path $RepoRoot 'stage1'
$Stage1BinDir = Join-Path $Stage1Dir 'bin'
$PerfLogDir = Join-Path $ArtifactsDir "log\$configuration\PerformanceLogs"

$GlobalJson = Get-Content -Raw -Path (Join-Path $RepoRoot 'global.json') | ConvertFrom-Json

# Mirror of tools.ps1 GetDefaultMSBuildEngine: presence of tools.vs => 'vs', else tools.dotnet => 'dotnet'.
if (-not $msbuildEngine) {
  if (Get-Member -InputObject $GlobalJson.tools -Name 'vs') {
    $msbuildEngine = 'vs'
  } elseif (Get-Member -InputObject $GlobalJson.tools -Name 'dotnet') {
    $msbuildEngine = 'dotnet'
  } else {
    Write-Host 'error: -msbuildEngine must be specified, or global.json must specify tools.dotnet or tools.vs.'
    exit 1
  }
}

# build.ps1 is invoked out-of-proc via the host process (pwsh.exe or powershell.exe). -File starts a fresh
# process so build.ps1 does not inherit this script's PowerShell scope. Args after -File are parsed as a
# command line, so named switches (-restore, -build, ...) bind correctly; passing the arg arrays via
# splatting (@args) keeps each element a distinct argument, preserving values that contain spaces.
$pwshPath = (Get-Process -Id $PID).Path
$buildScript = Join-Path $PSScriptRoot 'common\build.ps1'

# Arguments common to both the stage 1 and stage 2 builds, including any caller-supplied $properties.
$commonBuildArgs = @('-restore', '-build', '-ci', '-prepareMachine', '-configuration', $configuration, '-msbuildEngine', $msbuildEngine) + $properties

if ($buildStage1)
{
  Write-Host "& $buildScript $($commonBuildArgs -join ' ')"
  & $pwshPath -NoLogo -NoProfile -ExecutionPolicy ByPass -File $buildScript @commonBuildArgs
  if ($LASTEXITCODE -ne 0) {
    throw "Stage 1 build.ps1 failed with exit code $LASTEXITCODE"
  }

  # Move the stage 1 outputs aside so stage 2 gets a clean $ArtifactsDir to build into.
  if (Test-Path $Stage1Dir)
  {
    Remove-Item -Force -Recurse $Stage1Dir
  }

  Move-Item -Path $ArtifactsDir -Destination $Stage1Dir -Force
}

# Resolve the bootstrapped build tool AFTER the stage 1 outputs have been moved into $Stage1Dir, so the
# globbed paths below point at files that actually exist.
$bootstrapRoot = Join-Path $Stage1BinDir "bootstrap"

if ($msbuildEngine -eq 'vs')
{
  $buildToolPath = Join-Path $bootstrapRoot "net472\MSBuild\Current\Bin\MSBuild.exe"
  $buildToolCommand = "";
}
else
{
  $buildToolPath = "$bootstrapRoot\core\dotnet.exe"
  $buildToolCommand = "msbuild"
  $env:DOTNET_ROOT="$bootstrapRoot\core"
}

# Communicate the bootstrapped build tool to the (out-of-proc) stage 2 build.ps1 via environment
# variables so it does not require dot-sourcing tools.ps1 here. tools.ps1's InitializeBuildTool
# honors _BuildToolPath / _BuildToolCommand and only consumes Path and Command.
$env:_BuildToolPath    = $buildToolPath
$env:_BuildToolCommand = $buildToolCommand

# Ensure that debug bits fail fast, rather than hanging waiting for a debugger attach.
$env:MSBUILDDONOTLAUNCHDEBUGGER="true"

# Opt into performance logging. https://github.com/dotnet/msbuild/issues/5900
$env:DOTNET_PERFLOG_DIR=$PerfLogDir

# Point child processes (stage 2 build.ps1, tests, and the MSBuild grandchildren they spawn, notably
# net472 x86 testhosts invoking .NET Core MSBuild → /mt → sidecar TaskHost) at the freshly-built
# bootstrap .NET host, so task hosts launch with the bits under test. This matches DOTNET_ROOT (set
# above for the core engine) and the bootstrap's own expectation that tests invoke the bootstrap dotnet
# (see eng/BootStrapMsBuild.targets).
$env:DOTNET_HOST_PATH = Join-Path $bootstrapRoot 'core\dotnet.exe'
$env:DOTNET_INSTALL_DIR = Join-Path $bootstrapRoot 'core'

# $stage2Properties are appended to the stage 2 build only.
# Use this for switches like /mt that should not be passed to the stable MSBuild used in stage 1
# until a stable version of MT is available in the images.
# The @(...) wrapper is important: when -split returns exactly one element PowerShell gives back a
# string, which would later splat one character per argument (so "/mt" becomes "/", "m", "t").
# Wrapping with @() forces an array even for a single token.
$stage2Args = @(if ($stage2Properties) { $stage2Properties -split '\s+' | Where-Object { $_ } } else { @() })

# Stage 2 shares the common build args and additionally:
#   onlyDocChanged=1 → bootstrap not created (artifacts not needed downstream)
#   skipTests        → bootstrap IS created (downstream MAY consume it), tests omitted
#   default          → bootstrap created, tests run
$stage2BuildArgs = $commonBuildArgs
if ($onlyDocChanged) {
  $stage2BuildArgs += '/p:CreateBootstrap=false'
}
elseif (-not $skipTests) {
  $stage2BuildArgs += '-test'
}
$stage2BuildArgs += $stage2Args

Write-Host "& $buildScript $($stage2BuildArgs -join ' ')"
& $pwshPath -NoLogo -NoProfile -ExecutionPolicy ByPass -File $buildScript @stage2BuildArgs
exit $LASTEXITCODE
