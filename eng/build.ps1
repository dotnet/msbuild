[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $msbuildEngine,
  [string] $configuration = "Debug",
  [switch] $test,
  [switch] $ci,
  [switch][Alias('bl')] $binaryLog,
  [switch][Alias('nobl')] $excludeCIBinarylog,
  [switch] $stage2,
  [string[]][Alias('stage2Argument')] $stage2Arguments = @(),
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

$pwshPath = (Get-Process -Id $PID).Path
$buildScript = Join-Path $PSScriptRoot 'common\build.ps1'

# Arguments common to the stage1 and stage2 builds, including any caller-supplied $properties.
$commonBuildArgs = @('-configuration', $configuration) + $properties

# Forward the argument if supplied. Needs to be done for all above explicit parameters that should be passed to the build.
if ($msbuildEngine) {
  $commonBuildArgs += '-msbuildEngine'
  $commonBuildArgs += $msbuildEngine
}

# Forward the binary-log-related switches to both the stage 1 and stage 2 builds.
if ($ci) {
  $commonBuildArgs += '-ci'
}
if ($binaryLog) {
  $commonBuildArgs += '-binaryLog'
}
if ($excludeCIBinarylog) {
  $commonBuildArgs += '-excludeCIBinarylog'
}

# Supplying stage2Arguments implies a multi-stage build
if ($stage2Arguments.Count -gt 0) {
  $stage2 = $true
}

$buildArgs = $commonBuildArgs

# If the caller requested a multi-stage build, add the -prepareMachine switch to the stage 1 build so that it kills any lingering processes from stage 1 before stage 2 starts.
# Also disable the pipeline set result masking for stage 1 so that a stage 1 failure surfaces its real exit code to this wrapper (stage 2 is the terminal build that reports the pipeline result).
if ($stage2) {
  $buildArgs += '-prepareMachine'
  $buildArgs += '-disablePipelineSetResult'
}

if ($test -and -not $stage2) {
  $buildArgs += '-test'
}

# Log the stage 1 build command so that it's clear which arguments flow to it.
if ($stage2) {
  Write-Host "Stage 1 build: & `"$buildScript`" $buildArgs"
}

& $pwshPath -NoLogo -NoProfile -ExecutionPolicy ByPass -File "$buildScript" @buildArgs

if (-not $stage2) {
  exit $LASTEXITCODE
}

### END of stage1 build ###

if ($LASTEXITCODE -ne 0) {
  throw "Stage 1 build failed with exit code $LASTEXITCODE"
}

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$ArtifactsDir = Join-Path $RepoRoot 'artifacts'
$Stage1Dir = Join-Path $RepoRoot 'stage1'
$Stage1BinDir = Join-Path $Stage1Dir 'bin'
$PerfLogDir = Join-Path $ArtifactsDir "log\$configuration\PerformanceLogs"
$BootstrapRoot = Join-Path $Stage1BinDir "bootstrap"

# Clean a previous stage1 artifacts folder and move the stage 1 outputs aside so stage 2 gets a clean $ArtifactsDir to build into.
Remove-Item -Force -Recurse $Stage1Dir -ErrorAction SilentlyContinue
Move-Item -Path $ArtifactsDir -Destination $Stage1Dir -Force

# The move above relocated the stage 1 log directory (including its binlog) out of the published
# $ArtifactsDir\log location. Copy the whole log folder back so CI publishes the stage 1 logs alongside
# the stage 2 ones. This runs before the stage 2 build, so it won't clobber any stage 2 output. The
# stage 1 binlog keeps its default Build.binlog name, distinct from the stage 2 Build.stage2.binlog.
# Best-effort: never fail the build if the stage 1 log folder isn't there (e.g. when no logs were produced).
$stage1LogDir = Join-Path $Stage1Dir 'log'
if (Test-Path $stage1LogDir) {
  New-Item -ItemType Directory -Force -Path $ArtifactsDir | Out-Null
  Copy-Item -Path $stage1LogDir -Destination $ArtifactsDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Mirror of tools.ps1 GetDefaultMSBuildEngine: presence of tools.vs => 'vs', else tools.dotnet => 'dotnet'.
$GlobalJson = Get-Content -Raw -Path (Join-Path $RepoRoot 'global.json') | ConvertFrom-Json
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

if ($msbuildEngine -eq 'vs')
{
  $buildToolPath = Join-Path $BootstrapRoot "net472\MSBuild\Current\Bin\MSBuild.exe"
  $buildToolCommand = "";
}
else
{
  $buildToolPath = "$BootstrapRoot\core\dotnet.exe"
  $buildToolCommand = "msbuild"
  $env:DOTNET_ROOT="$BootstrapRoot\core"
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
$env:DOTNET_HOST_PATH = Join-Path $BootstrapRoot 'core\dotnet.exe'
$env:DOTNET_INSTALL_DIR = Join-Path $BootstrapRoot 'core'

# $stage2Arguments are appended to the stage 2 build only.
# Use this for switches like /mt that should not be passed to the stage1 build
# until a stable version of MT is available in the images.
$stage2Args = $stage2Arguments

$stage2BuildArgs = $commonBuildArgs

# Give the stage 2 binary log a distinct name so it doesn't collide with the stage 1 binlog
# (both otherwise default to Build.binlog) when CI publishes them to the same artifacts location.
# Only do this when a binary log will actually be produced, so we don't force one to be created:
# Arcade emits a binlog for CI builds (-ci) or when -binaryLog is passed explicitly, unless it's
# suppressed with -excludeCIBinarylog.
if (($ci -or $binaryLog) -and -not $excludeCIBinarylog) {
  $stage2BuildArgs += '-binaryLogName'
  $stage2BuildArgs += 'Build.stage2.binlog'
}

# Only run tests in stage2 when supplying the '-test' switch in a multi-stage build.
if ($test) {
  $stage2BuildArgs += '-test'
}

$stage2BuildArgs += $stage2Args

Write-Host "Stage 2 build: & `"$buildScript`" $stage2BuildArgs"
# Needs to run out-of-proc to not inherit the stage 1 build's state variables.
& $pwshPath -NoLogo -NoProfile -ExecutionPolicy ByPass -File "$buildScript" @stage2BuildArgs

exit $LASTEXITCODE
