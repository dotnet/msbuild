<#
.SYNOPSIS
    Runs MSBuild benchmarks across the supported runtime target frameworks.

.DESCRIPTION
    Runs BenchmarkDotNet benchmarks selected by filter or named set sequentially for each requested
    target framework. By default, the script runs net472 and net11.0 on Windows, and net11.0
    elsewhere. The .NET SDK selected by the repository is used for the benchmark host and its child
    processes. Artifacts from each run are written to a separate target-framework directory.

.PARAMETER Filter
    One or more BenchmarkDotNet filter patterns.

.PARAMETER Set
    One or more named benchmark sets. Multiple sets are combined with OR.

.PARAMETER All
    Runs every benchmark. This must be specified explicitly because a complete run can take a
    significant amount of time.

.PARAMETER TargetFramework
    Target frameworks to run. Defaults to net472 and net11.0 on Windows, and net11.0 elsewhere.

.PARAMETER Configuration
    The build configuration passed to dotnet run. Defaults to Release.

.PARAMETER Job
    The BenchmarkDotNet job to run, such as short or dry.

.PARAMETER LaunchCount
    Number of independent benchmark process launches. If omitted, BenchmarkDotNet uses its default.

.PARAMETER EnforcePowerPlan
    Allows BenchmarkDotNet to temporarily select the High Performance power plan on Windows.
    By default, the current host power plan is left unchanged.

.PARAMETER CollectEtw
    Enables ETW profiling diagnostics.

.PARAMETER DisableNGen
    Disables NGEN and ReadyToRun.

.PARAMETER DisableInlining
    Disables JIT inlining.

.PARAMETER ArtifactsPath
    The root directory for BenchmarkDotNet artifacts. Defaults to artifacts/BenchmarkDotNet at
    the repository root. Relative paths are resolved from the current PowerShell location. Each
    target framework uses a subdirectory.

.PARAMETER BenchmarkDotNetArguments
    Additional uncommon arguments passed to BenchmarkDotNet.

.EXAMPLE
    .\Run-Benchmarks.ps1 -Filter '*MetadataExpansionBenchmark*'

.EXAMPLE
    .\Run-Benchmarks.ps1 -Set Expansion

.EXAMPLE
    .\Run-Benchmarks.ps1 -Filter '*PropertyExpansionBenchmark*' `
        -Job short -DisableNGen

.EXAMPLE
    .\Run-Benchmarks.ps1 -Filter '*PropertyExpansionBenchmark*' `
        -Framework net11.0 -LaunchCount 3

.EXAMPLE
    .\Run-Benchmarks.ps1 -All
#>
[CmdletBinding(DefaultParameterSetName = 'Filtered')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Filtered')]
    [ValidateNotNullOrEmpty()]
    [string[]]$Filter,

    [Parameter(Mandatory, ParameterSetName = 'Set')]
    [ValidateSet(
        'Expansion',
        'PropertyExpansion',
        'PropertyExpansionScaling',
        'PropertyBagCardinality',
        'PropertyFunctions',
        'ItemExpansion',
        'ItemFunctions',
        'MetadataExpansion',
        'MetadataExpansionScaling',
        'MixedExpansion',
        'Scaling',
        'Conditions',
        'ConditionParsing',
        'ConditionEvaluation',
        'ExpressionShredder',
        'ExpressionShredderThroughput',
        'ExpressionShredderAllocations',
        'Items',
        'ItemEvaluation')]
    [string[]]$Set,

    [Parameter(Mandatory, ParameterSetName = 'All')]
    [switch]$All,

    [Alias('Framework')]
    [string[]]$TargetFramework,

    [ValidateNotNullOrEmpty()]
    [string]$Configuration = 'Release',

    [ValidateNotNullOrEmpty()]
    [string]$Job,

    [ValidateRange(1, 2147483647)]
    [int]$LaunchCount,

    [switch]$EnforcePowerPlan,

    [switch]$CollectEtw,

    [switch]$DisableNGen,

    [switch]$DisableInlining,

    [ValidateNotNullOrEmpty()]
    [string]$ArtifactsPath,

    [string[]]$BenchmarkDotNetArguments = @()
)

Set-StrictMode -Version 'Latest'
$ErrorActionPreference = 'Stop'

# Match Arcade's SDK selection without installing anything. A repository build uses an existing installation
# from DOTNET_INSTALL_DIR or PATH when it contains the requested SDK, and populates .dotnet only as a fallback.
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dotnetExecutable = if ($env:OS -eq 'Windows_NT') { 'dotnet.exe' } else { 'dotnet' }
$dotnetSdkVersion = (Get-Content (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).tools.dotnet
$dotnetRoots = @($env:DOTNET_INSTALL_DIR)
$installedDotnet = Get-Command $dotnetExecutable -CommandType Application -ErrorAction SilentlyContinue

if ($installedDotnet)
{
    $dotnetRoots += Split-Path $installedDotnet.Path -Parent
}

$dotnetRoots += Join-Path $repoRoot '.dotnet'
$dotnetRoot = $null
$dotnetPath = $null
$dotnetSdkPath = $null

foreach ($candidateRoot in $dotnetRoots)
{
    if (-not $candidateRoot)
    {
        continue
    }

    $candidateDotnetPath = Join-Path $candidateRoot $dotnetExecutable
    $candidateSdkPath = Join-Path (Join-Path $candidateRoot 'sdk') $dotnetSdkVersion

    if ((Test-Path $candidateDotnetPath -PathType Leaf) -and
        (Test-Path $candidateSdkPath -PathType Container))
    {
        $dotnetRoot = $candidateRoot
        $dotnetPath = $candidateDotnetPath
        $dotnetSdkPath = $candidateSdkPath
        break
    }
}

if (-not $dotnetPath)
{
    throw ".NET SDK '$dotnetSdkVersion' was not found. Run the repository build first."
}

Write-Host "Using .NET SDK: $dotnetSdkPath"

if (-not $ArtifactsPath)
{
    $ArtifactsPath = Join-Path (Join-Path $repoRoot 'artifacts') 'BenchmarkDotNet'
}

if (-not $TargetFramework)
{
    $TargetFramework = if ($env:OS -eq 'Windows_NT')
    {
        @('net472', 'net11.0')
    }
    else
    {
        @('net11.0')
    }
}

if ($All)
{
    $selectionArguments = @('--filter', '*')
}
elseif ($Set)
{
    $selectionArguments = @('--anyCategories') + $Set
}
else
{
    $selectionArguments = @('--filter') + $Filter
}

$scriptArguments = @{
    '--anyCategories' = '-Set'
    '--artifacts' = '-ArtifactsPath'
    '--collect-etw' = '-CollectEtw'
    '--disable-inlining' = '-DisableInlining'
    '--disable-ngen' = '-DisableNGen'
    '--enforce-power-plan' = '-EnforcePowerPlan'
    '--filter' = '-Filter'
    '--job' = '-Job'
    '--launchCount' = '-LaunchCount'
}

foreach ($scriptArgument in $scriptArguments.GetEnumerator())
{
    if ($BenchmarkDotNetArguments -contains $scriptArgument.Key)
    {
        throw "Use $($scriptArgument.Value) instead of passing '$($scriptArgument.Key)' through -BenchmarkDotNetArguments."
    }
}

$projectPath = Join-Path $PSScriptRoot 'MSBuild.Benchmarks.csproj'
$artifactsRoot = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ArtifactsPath)

# BenchmarkDotNet launches child dotnet hosts and generated apphosts. Pin each host-selection mechanism to the
# resolved installation so generated projects do not silently use a different machine-wide SDK or runtime.
$architecture = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToUpperInvariant()
$dotnetRootArchitectureVariable = "DOTNET_ROOT_$architecture"
$dotnetEnvironment = @{
    'DOTNET_HOST_PATH' = $dotnetPath
    'DOTNET_INSTALL_DIR' = $dotnetRoot
    'DOTNET_ROOT' = $dotnetRoot
    $dotnetRootArchitectureVariable = $dotnetRoot
}

# PowerShell scripts run in the caller's process, so restore its SDK selection after the benchmarks finish.
$originalDotnetEnvironment = @{}

foreach ($variable in $dotnetEnvironment.Keys)
{
    $originalDotnetEnvironment[$variable] =
        [Environment]::GetEnvironmentVariable($variable, [EnvironmentVariableTarget]::Process)
    [Environment]::SetEnvironmentVariable(
        $variable,
        $dotnetEnvironment[$variable],
        [EnvironmentVariableTarget]::Process)
}

$originalPath = $env:PATH
$env:PATH = "$dotnetRoot$([IO.Path]::PathSeparator)$originalPath"

try
{
    foreach ($framework in $TargetFramework)
    {
        $frameworkArtifactsPath = Join-Path $artifactsRoot $framework
        $dotnetArguments = @(
            'run'
            '--project'
            $projectPath
            '--configuration'
            $Configuration
            '--framework'
            $framework
            '--'
        )

        $dotnetArguments += $selectionArguments
        $dotnetArguments += @('--artifacts', $frameworkArtifactsPath)

        if ($Job)
        {
            $dotnetArguments += @('--job', $Job)
        }

        if ($PSBoundParameters.ContainsKey('LaunchCount'))
        {
            $dotnetArguments += @('--launchCount', $LaunchCount)
        }

        if ($EnforcePowerPlan)
        {
            $dotnetArguments += '--enforce-power-plan'
        }

        if ($CollectEtw)
        {
            $dotnetArguments += '--collect-etw'
        }

        if ($DisableNGen)
        {
            $dotnetArguments += '--disable-ngen'
        }

        if ($DisableInlining)
        {
            $dotnetArguments += '--disable-inlining'
        }

        $dotnetArguments += $BenchmarkDotNetArguments

        Write-Host "`nRunning MSBuild benchmarks for $framework"
        Write-Host "Artifacts: $frameworkArtifactsPath"

        & $dotnetPath @dotnetArguments

        if ($LASTEXITCODE -ne 0)
        {
            throw "Benchmarks for '$framework' failed with exit code $LASTEXITCODE."
        }
    }

    Write-Host "`nAll requested benchmark runs completed."
    Write-Host "Artifacts: $artifactsRoot"
}
finally
{
    $env:PATH = $originalPath

    foreach ($variable in $originalDotnetEnvironment.Keys)
    {
        [Environment]::SetEnvironmentVariable(
            $variable,
            $originalDotnetEnvironment[$variable],
            [EnvironmentVariableTarget]::Process)
    }
}
