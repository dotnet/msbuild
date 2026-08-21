<#
.SYNOPSIS
  Collects the evidence needed to investigate an -mt build-equivalence failure after the fact.

.DESCRIPTION
  The artifact snapshots are far too large to publish (roughly 4.5 GB each, three of them), but
  without *something* a failing run leaves only a list of paths - and a regression that reproduces
  intermittently may never reproduce on a developer machine. This collects a bounded evidence bundle:

    binlogs/<run>.binlog          the binary log of each build, so the failure can be traced to the
                                  task and target that produced the differing file
    evidence/<run>/<path>         both versions of every differing file, so the bytes can be diffed
                                  offline without rerunning anything
    evidence-manifest.json        what was collected, what was skipped, and why

  Differences are collected in priority order - the ones attributed to -mt first, then the rest of the
  mt comparison, then the control comparison - so that when a cap is hit, the most important evidence
  is what survives.

.PARAMETER WorkDir
  The directory holding the baseline/, mt/ and control/ snapshots plus reports/.

.PARAMETER MaxFiles
  Maximum number of file *pairs* to collect. A systemic regression could differ in thousands of files;
  the first few are enough to diagnose it and the manifest records the true total.

.PARAMETER MaxTotalBytes
  Overall size cap for collected file pairs. Binlogs are always collected and do not count against it.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $WorkDir,
    [string] $OutputDir,
    [int]    $MaxFiles = 200,
    [long]   $MaxTotalBytes = 500MB
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$reportsDir = Join-Path $WorkDir 'reports'
if (-not $OutputDir) { $OutputDir = $reportsDir }
$binlogDir = Join-Path $OutputDir 'binlogs'
$evidenceDir = Join-Path $OutputDir 'evidence'
New-Item -ItemType Directory -Force -Path $binlogDir | Out-Null

function Get-Report {
    param([string] $Name)

    $path = Join-Path $reportsDir $Name
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

# ---------------------------------------------------------------------------------------------
# Binary logs. Always collected: they are small next to the snapshots, and they are the only way to
# trace a differing file back to the task that wrote it.
# ---------------------------------------------------------------------------------------------

$collectedBinlogs = @()
foreach ($run in @('baseline', 'mt', 'control')) {
    $snapshot = Join-Path $WorkDir $run
    if (-not (Test-Path -LiteralPath $snapshot)) { continue }
    $binlog = Get-ChildItem -LiteralPath (Join-Path $snapshot 'log') -Recurse -Filter '*.binlog' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $binlog) { continue }
    Copy-Item -LiteralPath $binlog.FullName -Destination (Join-Path $binlogDir "$run.binlog") -Force
    $collectedBinlogs += [pscustomobject]@{ Run = $run; Bytes = $binlog.Length }
}

# ---------------------------------------------------------------------------------------------
# Differing files, in priority order.
# ---------------------------------------------------------------------------------------------

$verdict = Get-Report -Name 'verdict.json'
$mtReport = Get-Report -Name 'artifact-compare.mt-vs-baseline.json'
$controlReport = Get-Report -Name 'artifact-compare.control.json'

$unexplained = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
if ($verdict -and $verdict.PSObject.Properties.Name -contains 'UnexplainedArtifacts') {
    foreach ($p in @($verdict.UnexplainedArtifacts)) { if ($p) { [void]$unexplained.Add([string]$p) } }
}

$queue = New-Object System.Collections.Generic.List[object]
$queued = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

function Add-Candidate {
    param([string] $Rel, [string] $CandidateRun, [string] $Priority)

    $key = "$CandidateRun|$Rel"
    if (-not $queued.Add($key)) { return }
    $queue.Add([pscustomobject]@{ Rel = $Rel; CandidateRun = $CandidateRun; Priority = $Priority })
}

# 1. Attributed to -mt: the reason the run is red.
foreach ($rel in $unexplained) { Add-Candidate -Rel $rel -CandidateRun 'mt' -Priority 'attributed-to-mt' }
# 2. The rest of the mt comparison's unexpected differences.
if ($mtReport) { foreach ($d in @($mtReport.Failures)) { if ($d) { Add-Candidate -Rel $d.Rel -CandidateRun 'mt' -Priority 'mt-comparison' } } }
# 3. The control comparison's unexpected differences: pre-existing non-determinism worth fixing.
if ($controlReport) { foreach ($d in @($controlReport.Failures)) { if ($d) { Add-Candidate -Rel $d.Rel -CandidateRun 'control' -Priority 'control-comparison' } } }

$collected = New-Object System.Collections.Generic.List[object]
$skipped = New-Object System.Collections.Generic.List[object]
$totalBytes = 0L

foreach ($item in $queue) {
    if ($collected.Count -ge $MaxFiles) {
        $skipped.Add([pscustomobject]@{ Rel = $item.Rel; Run = $item.CandidateRun; Reason = 'file-count cap reached' })
        continue
    }

    $relNative = $item.Rel -replace '/', '\'
    $pair = @(
        [pscustomobject]@{ Run = 'baseline'; Path = Join-Path (Join-Path $WorkDir 'baseline') $relNative }
        [pscustomobject]@{ Run = $item.CandidateRun; Path = Join-Path (Join-Path $WorkDir $item.CandidateRun) $relNative }
    )

    $pairBytes = 0L
    foreach ($side in $pair) { if (Test-Path -LiteralPath $side.Path) { $pairBytes += (Get-Item -LiteralPath $side.Path).Length } }
    if (($totalBytes + $pairBytes) -gt $MaxTotalBytes) {
        $skipped.Add([pscustomobject]@{ Rel = $item.Rel; Run = $item.CandidateRun; Reason = "size cap reached (pair is $([math]::Round($pairBytes / 1MB, 1)) MB)" })
        continue
    }

    $copiedSides = @()
    foreach ($side in $pair) {
        if (-not (Test-Path -LiteralPath $side.Path)) { continue }
        $destination = Join-Path (Join-Path $evidenceDir $side.Run) $relNative
        New-Item -ItemType Directory -Force -Path (Split-Path $destination -Parent) | Out-Null
        Copy-Item -LiteralPath $side.Path -Destination $destination -Force
        $copiedSides += $side.Run
    }

    $totalBytes += $pairBytes
    $collected.Add([pscustomobject]@{ Rel = $item.Rel; Priority = $item.Priority; Sides = $copiedSides; Bytes = $pairBytes })
}

# ---------------------------------------------------------------------------------------------
# Manifest
# ---------------------------------------------------------------------------------------------

$manifest = [pscustomobject]@{
    GeneratedUtc        = (Get-Date).ToUniversalTime().ToString('o')
    WorkDir             = $WorkDir
    Binlogs             = $collectedBinlogs
    CollectedPairCount  = $collected.Count
    CollectedBytes      = $totalBytes
    SkippedCount        = $skipped.Count
    MaxFiles            = $MaxFiles
    MaxTotalBytes       = $MaxTotalBytes
    Collected           = $collected.ToArray()
    Skipped             = $skipped.ToArray()
    Note                = 'evidence/<run>/<path> holds both versions of each differing file. binlogs/<run>.binlog is the build log that produced it. The full artifact snapshots are not published: they are roughly 4.5 GB each.'
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDir 'evidence-manifest.json') -Encoding utf8

Write-Host ("[evidence] {0} binlog(s); {1} differing file pair(s) collected ({2:n1} MB); {3} skipped by caps" -f `
        $collectedBinlogs.Count, $collected.Count, ($totalBytes / 1MB), $skipped.Count)
if ($skipped.Count -gt 0) {
    Write-Host "[evidence] caps: MaxFiles=$MaxFiles, MaxTotalBytes=$([math]::Round($MaxTotalBytes / 1MB))MB. See evidence-manifest.json for the full list of differences."
}
