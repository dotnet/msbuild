<#
.SYNOPSIS
    Collects a CPU profile of MSBuild project evaluation and folds it into cost categories.

.DESCRIPTION
    Runs the evaluation analysis harness in its profiling-only mode (warm up, then nothing but cold
    evaluations) under dotnet-trace, then folds the resulting speedscope profile into categories with
    fold-evaluation-profile.py.

    This is the lens that attributes time the event source markers cannot see, such as which syscall
    inside LoadDocument is expensive, or how much of an evaluation is garbage collection.

    Requires the repository to have been built (build.cmd -configuration Release) so that the
    bootstrap SDK exists, plus dotnet-trace and Python 3.

.PARAMETER Project
    An existing, restored project to evaluate. When omitted, the harness creates and restores a
    temporary 'dotnet new console' project.

.PARAMETER Iterations
    Number of cold evaluations to profile. More iterations give a smoother profile.

.PARAMETER OutputDirectory
    Where to write the trace and the folded report.

.EXAMPLE
    ./Collect-EvaluationProfile.ps1 -Iterations 120
#>
[CmdletBinding()]
param(
    [string] $Project,
    [int] $Iterations = 100,
    [string] $OutputDirectory = (Join-Path $PSScriptRoot '../../../artifacts/evaluation-profile')
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '../../..')
$dotnet = Join-Path $repoRoot '.dotnet/dotnet.exe'
$harness = Join-Path $repoRoot 'artifacts/bin/MSBuild.Benchmarks/Release/net11.0/MSBuild.Benchmarks.dll'
$folder = Join-Path $PSScriptRoot 'fold-evaluation-profile.py'

if (-not (Test-Path $harness)) {
    throw "Harness not found at '$harness'. Build the repository first: ./build.cmd -configuration Release"
}

$dotnetTrace = Get-Command dotnet-trace -ErrorAction SilentlyContinue
if (-not $dotnetTrace) {
    $candidate = Join-Path $env:USERPROFILE '.dotnet/tools/dotnet-trace.exe'
    if (-not (Test-Path $candidate)) {
        throw "dotnet-trace not found. Install it with: dotnet tool install --global dotnet-trace"
    }
    $dotnetTrace = $candidate
}
else {
    $dotnetTrace = $dotnetTrace.Source
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$trace = Join-Path $OutputDirectory 'evaluation.nettrace'
$speedscope = Join-Path $OutputDirectory 'evaluation.speedscope.json'
$report = Join-Path $OutputDirectory 'evaluation-profile.txt'

$harnessArgs = @($harness, '--analyze', '--profile-only', '--iterations', $Iterations)
if ($Project) {
    $harnessArgs += @('--project', $Project)
}

Write-Host "Collecting profile ($Iterations cold evaluations)..."
& $dotnetTrace collect --format speedscope --output $trace -- $dotnet @harnessArgs

if (-not (Test-Path $speedscope)) {
    throw "dotnet-trace did not produce '$speedscope'."
}

Write-Host "Folding profile into categories..."
& python $folder $speedscope | Tee-Object -FilePath $report

Write-Host ""
Write-Host "Trace:  $trace"
Write-Host "Report: $report"
