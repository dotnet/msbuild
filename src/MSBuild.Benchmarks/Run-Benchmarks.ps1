<#
.SYNOPSIS
    Runs MSBuild benchmarks across the supported runtime target frameworks.

.DESCRIPTION
    Runs BenchmarkDotNet benchmarks selected by filter or named set sequentially for each requested
    target framework. By default, the script runs net472 and net11.0 on Windows, and net11.0
    elsewhere. Artifacts from each run are written to a separate target-framework directory.

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
    the repository root. Each target framework uses a subdirectory.

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

if (-not $ArtifactsPath)
{
    $repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
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
$artifactsRoot = [System.IO.Path]::GetFullPath($ArtifactsPath)

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

    & dotnet @dotnetArguments

    if ($LASTEXITCODE -ne 0)
    {
        throw "Benchmarks for '$framework' failed with exit code $LASTEXITCODE."
    }
}

Write-Host "`nAll requested benchmark runs completed."
Write-Host "Artifacts: $artifactsRoot"
