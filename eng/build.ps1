# Only parameters that this script has to see itself are declared here; everything else flows through
# $properties untouched and is forwarded verbatim, so Arcade parameters keep working without being
# mirrored. Two of them are declared purely to stop PowerShell from claiming the argument first:
# without '-verbosity', '-v minimal' binds to the common '-Verbose' switch and 'minimal' arrives as a
# bare property, and without '-build', Arcade's '-b' prefix-matches '-binaryLog' and silently turns a
# build request into a binary log request.
# Keep this list to [string] and [switch] parameters. A [bool] parameter (Arcade has several, such as
# -nodeReuse and -msbuildMultiThreaded) would need special handling, because it forwards as 'True'
# or 'False', which re-parses as a string rather than as a boolean; leaving those undeclared lets
# Arcade bind them from $properties directly, as '-nodeReuse 0' or '-nodeReuse $false'.
[CmdletBinding(PositionalBinding=$false)]
Param(
  [string][Alias('c')] $configuration = "Debug",
  [string][Alias('v')] $verbosity,
  [string] $msbuildEngine,
  [switch][Alias('b')] $build,
  [switch][Alias('t')] $test,
  [switch] $ci,
  [switch][Alias('bl')] $binaryLog,
  [string][Alias('bln')] $binaryLogName,
  [switch][Alias('nobl')] $excludeCIBinarylog,
  [switch] $stage2,
  [string[]] $stage2Argument = @(),
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

$pwshPath = (Get-Process -Id $PID).Path
$buildScript = Join-Path $PSScriptRoot 'common\build.ps1'

# The build script path is single-quoted for the `pwsh -Command` re-parse below, so any single quote
# in the repo path has to be doubled to stay inside the quoted string.
$quotedBuildScript = "'" + ($buildScript -replace "'", "''") + "'"

# The stage builds run out-of-proc so that stage 2 doesn't inherit stage 1's state variables, but
# they are invoked through `pwsh -Command` rather than `pwsh -File`. `-File` passes every argument
# as a literal string, which typed parameters such as `[bool] $nodeReuse` and `[bool] $msbuildMultiThreaded`
# can't bind to. `-Command` re-parses the arguments as PowerShell, so `-mt 1` arrives as an integer.
# Quote anything that isn't a simple token so it survives that re-parse intact
# (e.g. '-warnnotaserror NU1901;NU1902;NU1903', where ';' would otherwise separate statements).
function Get-QuotedArguments([string[]] $arguments) {
  $quoted = $arguments | ForEach-Object {
    if ($_ -match '^[\w\-/\\.:=+]+$') { $_ } else { "'" + ($_ -replace "'", "''") + "'" }
  }
  $quoted -join ' '
}

function Get-BuildCommand([string[]] $arguments) {
  # Default to a failure exit code so that an error which prevents the script from running at all
  # (e.g. a parameter binding failure) isn't reported as success by the trailing `exit`.
  "`$global:LASTEXITCODE = 1`n& $quotedBuildScript $(Get-QuotedArguments $arguments)`nexit `$LASTEXITCODE"
}

# The command as it's displayed to the user: just the invocation, without the exit code plumbing
# that Get-BuildCommand wraps around it.
function Get-BuildCommandForDisplay([string[]] $arguments) {
  "& $quotedBuildScript $(Get-QuotedArguments $arguments)"
}

# Forward every explicitly supplied parameter to the Arcade build. Apart from -configuration, which is
# always passed on because this script uses it for its own paths, only bound parameters are forwarded,
# so parameters the caller didn't pass keep their Arcade defaults instead of being pinned to this
# script's. Parameters listed here are handled by this script and must not be forwarded verbatim.
$locallyHandledParameters = @('configuration', 'test', 'stage2', 'stage2Argument', 'binaryLogName', 'properties')

function Get-ForwardedArguments($boundParameters) {
  $forwarded = @()
  foreach ($parameter in $boundParameters.GetEnumerator()) {
    if (($locallyHandledParameters -contains $parameter.Key) -or
        ([System.Management.Automation.PSCmdlet]::CommonParameters -contains $parameter.Key) -or
        ([System.Management.Automation.PSCmdlet]::OptionalCommonParameters -contains $parameter.Key)) {
      continue
    }

    $value = $parameter.Value
    if ($value -is [switch]) {
      # Every Arcade switch defaults to off, so an explicit '-switch:$false' needs nothing forwarded.
      if ($value.IsPresent) {
        $forwarded += "-$($parameter.Key)"
      }
    }
    else {
      $forwarded += "-$($parameter.Key)"
      $forwarded += [string] $value
    }
  }

  $forwarded
}

# Arguments common to the stage1 and stage2 builds, including any caller-supplied $properties.
# The binary log name is deliberately not part of this set: each stage gets its own name below.
$commonBuildArgs = @('-configuration', $configuration) + (Get-ForwardedArguments $PSBoundParameters)

# Guarded because $properties is $null rather than an empty array when no extra arguments were passed,
# which would otherwise append an empty argument to the build command.
if ($properties) {
  $commonBuildArgs += $properties
}

# Supplying stage2Arguments implies a multi-stage build
if ($stage2Argument.Count -gt 0) {
  $stage2 = $true
}

$buildArgs = $commonBuildArgs

if ($binaryLogName) {
  $buildArgs += '-binaryLogName'
  $buildArgs += $binaryLogName
}

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
$stage1Command = Get-BuildCommand $buildArgs
Write-Host "Stage 1 build: $(Get-BuildCommandForDisplay $buildArgs)"

& $pwshPath -NoLogo -NoProfile -ExecutionPolicy ByPass -Command $stage1Command

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

# $stage2Argument are appended to the stage 2 build only.
# Use this for switches like /mt that should not be passed to the stage1 build
# until a stable version of MT is available in the images.
$stage2Args = $stage2Argument

$stage2BuildArgs = $commonBuildArgs

# Give the stage 2 binary log a distinct name so it doesn't collide with the stage 1 binlog
# (both otherwise default to Build.binlog) when CI publishes them to the same artifacts location.
# Only do this when a binary log will actually be produced, so we don't force one to be created:
# Arcade emits a binlog for CI builds (-ci) or when -binaryLog is passed explicitly, unless it's
# suppressed with -excludeCIBinarylog.
if (($ci -or $binaryLog) -and -not $excludeCIBinarylog) {
  $stage2BuildArgs += '-binaryLogName'
  $stage2BuildArgs += if ($binaryLogName) {
    "$([System.IO.Path]::GetFileNameWithoutExtension($binaryLogName)).stage2$([System.IO.Path]::GetExtension($binaryLogName))"
  } else {
    'Build.stage2.binlog'
  }
}

# Only run tests in stage2 when supplying the '-test' switch in a multi-stage build.
if ($test) {
  $stage2BuildArgs += '-test'
}

$stage2BuildArgs += $stage2Args

$stage2Command = Get-BuildCommand $stage2BuildArgs
Write-Host "Stage 2 build: $(Get-BuildCommandForDisplay $stage2BuildArgs)"
# Needs to run out-of-proc to not inherit the stage 1 build's state variables.
& $pwshPath -NoLogo -NoProfile -ExecutionPolicy ByPass -Command $stage2Command

exit $LASTEXITCODE
