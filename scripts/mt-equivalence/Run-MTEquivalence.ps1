<#
.SYNOPSIS
  Proves that building MSBuild's official artifact set with MSBuild running -mt produces the same
  outputs as building it without -mt.

.DESCRIPTION
  Runs the *same* official-style build command up to three times in the *same* working tree (so that
  every embedded absolute path is identical) and compares the resulting artifact trees:

    baseline  build.cmd <official args>
    mt        build.cmd <official args>   with MSBUILD_MT_ENABLED=1, which is how arcade's
                                          eng/common/tools.ps1 adds -mt to the MSBuild command line
    control   build.cmd <official args>   again, with no -mt anywhere

  The control run is the scientific control: it measures how much the repo's build output varies
  between two *identical* builds. Any difference that also shows up in the control is a pre-existing
  build non-determinism and not something -mt caused. Without it, an -mt-vs-baseline diff cannot be
  interpreted, so the control is on by default.

  Each pair is compared twice:
    * Compare-Artifacts.ps1 - every produced file, byte for byte.
    * Compare-Binlogs.ps1   - the build logs, functionally (and asserts from the recorded MSBuild
                              command line that the mt run really did run with -mt).

.PARAMETER SkipBuilds
  Re-run only the comparisons against snapshots already present in -WorkDir. Use this when iterating
  on the comparison rules.

.EXAMPLE
  pwsh scripts/mt-equivalence/Run-MTEquivalence.ps1 -WorkDir D:\mtcmp
#>
[CmdletBinding()]
param(
    [string]   $RepoRoot,
    [string]   $WorkDir,
    [string]   $Configuration = 'Release',
    [string]   $OfficialBuildId,
    [string]   $MSBuildEngine = '',
    [string]   $VisualStudioDropName,
    [string[]] $ExtraBuildArgs = @(),
    [switch]   $SkipControl,
    [switch]   $SkipBuilds,
    [switch]   $DeepLogCompare
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

if (-not $RepoRoot) { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path }
if (-not $WorkDir) { $WorkDir = Join-Path $RepoRoot 'artifacts-mt-equivalence' }
if (-not $OfficialBuildId) { $OfficialBuildId = '{0}.1' -f (Get-Date).ToString('yyyyMMdd') }
# Mirrors VisualStudio.DropName in azure-pipelines/.vsts-dotnet-build-jobs.yml. The value is only
# embedded verbatim into the generated VS insertion manifests - nothing is uploaded - but it must be
# supplied or AfterSigning.proj fails, and it must be identical across the runs being compared.
if (-not $VisualStudioDropName) { $VisualStudioDropName = "Products/DevDiv/dotnet-msbuild/mt-equivalence/$OfficialBuildId" }

$ArtifactsDir = Join-Path $RepoRoot 'artifacts'
$Stage1Dir = Join-Path $RepoRoot 'stage1'
$ReportsDir = Join-Path $WorkDir 'reports'
New-Item -ItemType Directory -Force -Path $WorkDir, $ReportsDir | Out-Null

# The official build (azure-pipelines/.vsts-dotnet-build-jobs.yml) runs:
#   build.cmd -pack -sign -publish -ci -configuration $(BuildConfiguration) $(SkipApplyOptimizationDataArg) ...
#
# -publish IS passed. It is the official-build publish, not `dotnet publish`, and with
# DotNetPublishUsingPipelines=true it pushes nothing to any feed: it emits ##vso[artifact.upload]
# logging commands and produces three real outputs that would otherwise never be built - the Build
# Asset Registry manifest (artifacts/log/<config>/AssetManifest), the generated symbol packages
# (artifacts/tmp/<config>/SymbolPackages) and the staged PDBs (artifacts/tmp/<config>/PDBsToPublish).
# Feed publishing happens in the separate post-build stage, which this pipeline does not run.
#
# Dropped here:
#   -sign                       needs the MicroBuild signing plugin, and an Authenticode signature
#                               embeds a trusted-timestamp countersignature, so signed binaries could
#                               never be byte-identical between two runs. What -mt could actually
#                               affect is the content being signed, and that is compared unsigned.
#   EnableNgenOptimization      this is exactly $(SkipApplyOptimizationDataArg), which the official
#                               pipeline passes whenever OptProf is disabled. Leaving it on requires a
#                               VisualStudioDropAccessToken and downloads an IBC drop; it contributes
#                               nothing to an equivalence check because both runs would consume the
#                               same drop.
#   GenerateSbom                SBOM manifests embed a generation timestamp and a per-run GUID, so
#                               they can never be compared byte for byte anyway.
# Everything else is kept, so the same projects, targets and packaging run. With -MSBuildEngine vs
# this includes the VS insertion outputs (artifacts/VSSetup: .vsix, .vsman, the VS.ExternalAPIs
# package), which is why vs - what the official job uses - is the more meaningful engine to validate.
$officialArgs = @(
    '-ci'
    '-pack'
    '-publish'
    '-configuration', $Configuration
    '-verbosity', 'minimal'
    "/p:OfficialBuildId=$OfficialBuildId"
    '/p:RepositoryName=dotnet/msbuild'
    '/p:TeamName=MSBuild'
    '/p:DotNetPublishUsingPipelines=true'
    '/p:SuppressFinalPackageVersion=true'
    '/p:IsExperimental=true'
    '/p:EnableNgenOptimization=false'
    '/p:GenerateSbom=false'
    "/p:VisualStudioDropName=$VisualStudioDropName"
)
if ($MSBuildEngine) { $officialArgs = @('-msbuildEngine', $MSBuildEngine) + $officialArgs }
$officialArgs += $ExtraBuildArgs

function Remove-BuildOutputs {
    foreach ($dir in @($ArtifactsDir, $Stage1Dir)) {
        if (-not (Test-Path -LiteralPath $dir)) { continue }
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try {
                Remove-Item -LiteralPath $dir -Recurse -Force
                break
            }
            catch {
                if ($attempt -eq 5) { throw }
                Write-Host "  (retrying removal of $dir : $($_.Exception.Message))"
                Start-Sleep -Seconds 5
            }
        }
    }
}

function Invoke-OfficialStyleBuild {
    param([string] $Name, [bool] $MultiThreaded)

    $destination = Join-Path $WorkDir $Name
    Write-Host ''
    Write-Host "=== $Name build (MSBUILD_MT_ENABLED=$([int]$MultiThreaded)) ==="
    Write-Host "    build.cmd $($officialArgs -join ' ')"

    Remove-BuildOutputs
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }

    $previous = $env:MSBUILD_MT_ENABLED
    try {
        if ($MultiThreaded) { $env:MSBUILD_MT_ENABLED = '1' } else { Remove-Item Env:\MSBUILD_MT_ENABLED -ErrorAction SilentlyContinue }

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        # An Azure DevOps agent interprets ##vso[...] logging commands found in this output, and these
        # are full arcade official-style builds: with -publish they emit ##vso[artifact.upload] for
        # every package, symbol package, PDB and the asset manifest, and arcade's initialization emits
        # ##vso[task.setvariable] / ##vso[task.prependpath]. Letting the agent act on them is wrong in
        # three separate ways:
        #   * this validation pipeline would publish build assets (PackageArtifacts, BlobArtifacts,
        #     AssetManifests, PdbArtifacts) that only the real official build may publish;
        #   * the outer job's TEMP, TMP and PATH would be mutated by an inner build;
        #   * the agent uploads asynchronously, so it races the snapshot move below and fails the job
        #     with FileNotFoundException once the files have been moved aside.
        # Neutralize the marker. The agent scans for the "##vso[" token *anywhere* in a line, not just
        # at the start, so the token itself has to be broken - prefixing the line is not enough (it
        # leaves the agent half-parsing the command and erroring twice per line). Rewriting "##" to
        # "~~" keeps the line fully readable while making it inert.
        # Out-Host keeps the build's console output on stdout instead of letting it become part of
        # this function's return value.
        & (Join-Path $RepoRoot 'build.cmd') @officialArgs |
            ForEach-Object { [string]$_ -replace '##(?=(?:vso)?\[)', '~~' } |
            Out-Host
        $exit = $LASTEXITCODE
        $sw.Stop()
    }
    finally {
        if ($null -ne $previous) { $env:MSBUILD_MT_ENABLED = $previous }
        else { Remove-Item Env:\MSBUILD_MT_ENABLED -ErrorAction SilentlyContinue }
    }

    Write-Host ("    exit code $exit in {0:hh\:mm\:ss}" -f $sw.Elapsed)
    if ($exit -ne 0) {
        # A failed build is never snapshotted, so its binary log would be destroyed by the next run's
        # cleanup. Rescue it first: "-mt broke the build" is the most likely real failure, and the
        # binlog is the only way to diagnose it after the fact.
        try {
            $failureLogDir = Join-Path $ReportsDir 'binlogs'
            New-Item -ItemType Directory -Force -Path $failureLogDir | Out-Null
            $binlog = Get-ChildItem -LiteralPath (Join-Path $ArtifactsDir 'log') -Recurse -Filter '*.binlog' -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($binlog) {
                Copy-Item -LiteralPath $binlog.FullName -Destination (Join-Path $failureLogDir "$Name.FAILED.binlog") -Force
                Write-Host "    preserved the failing build's binary log: $(Join-Path $failureLogDir "$Name.FAILED.binlog")"
            }
        }
        catch {
            Write-Host "    warning: could not preserve the failing build's binary log: $($_.Exception.Message)"
        }

        if ($MultiThreaded) {
            # The most likely cause of "only the -mt build failed" is that the MSBuild driving the
            # build predates -mt (MSB1001 for an unrecognized switch). Say so, rather than leaving the
            # reader to infer it from a build log they have to go find.
            Write-Host 'note: only the -mt build failed. Check the log for MSB1001 (unrecognized switch): the MSBuild driving this build must itself support -mt.'
        }
        throw "The '$Name' build failed with exit code $exit."
    }
    if (-not (Test-Path -LiteralPath $ArtifactsDir)) { throw "The '$Name' build produced no artifacts directory." }

    Move-Item -LiteralPath $ArtifactsDir -Destination $destination -Force
    return [pscustomobject]@{ Name = $Name; Path = $destination; Duration = $sw.Elapsed }
}

$runs = @('baseline', 'mt')
if (-not $SkipControl) { $runs += 'control' }

$buildResults = @{}

if (-not $SkipBuilds) {
    # No warm-up pass: LogNormalizationRules.json drops every NuGet restore message precisely because
    # package-cache warmth is not a property of the build engine, and the artifact comparison is
    # unaffected by it.
    foreach ($run in $runs) {
        $buildResults[$run] = Invoke-OfficialStyleBuild -Name $run -MultiThreaded ($run -eq 'mt')
    }
}
else {
    foreach ($run in $runs) {
        $path = Join-Path $WorkDir $run
        if (-not (Test-Path -LiteralPath $path)) { throw "-SkipBuilds was passed but '$path' does not exist." }
        $buildResults[$run] = [pscustomobject]@{ Name = $run; Path = $path; Duration = [TimeSpan]::Zero }
    }
}

# ---------------------------------------------------------------------------------------------
# Comparisons
# ---------------------------------------------------------------------------------------------

# Replay binlogs with the MSBuild that the build itself produced: it is guaranteed to understand the
# binlog format version written by that build.
$replayHost = Join-Path $buildResults['baseline'].Path 'bin\bootstrap\core\dotnet.exe'
$replayCommand = 'msbuild'
if (-not (Test-Path -LiteralPath $replayHost)) {
    $replayHost = Join-Path $RepoRoot '.dotnet\dotnet.exe'
    Write-Host "note: no bootstrap dotnet in the baseline snapshot; replaying binlogs with $replayHost"
}

function Get-BinlogPath {
    param([string] $SnapshotPath)

    $candidate = Join-Path $SnapshotPath "log\$Configuration\Build.binlog"
    if (Test-Path -LiteralPath $candidate) { return $candidate }
    $found = Get-ChildItem -LiteralPath (Join-Path $SnapshotPath 'log') -Recurse -Filter '*.binlog' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { return $found.FullName }
    throw "No binlog found under $SnapshotPath\log."
}

$comparisons = @(
    [pscustomobject]@{ Label = 'mt-vs-baseline'; Baseline = 'baseline'; Candidate = 'mt'; ExpectMt = $true }
)
if (-not $SkipControl) {
    $comparisons += [pscustomobject]@{ Label = 'control'; Baseline = 'baseline'; Candidate = 'control'; ExpectMt = $false }
}

$outcomes = New-Object System.Collections.Generic.List[object]

foreach ($c in $comparisons) {
    Write-Host ''
    Write-Host "=== comparing $($c.Candidate) against $($c.Baseline) [$($c.Label)] ==="

    & (Join-Path $PSScriptRoot 'Compare-Artifacts.ps1') `
        -BaselineDir $buildResults[$c.Baseline].Path `
        -CandidateDir $buildResults[$c.Candidate].Path `
        -OutputDir $ReportsDir `
        -Label $c.Label
    $artifactExit = $LASTEXITCODE

    $logArgs = @{
        BaselineBinlog  = (Get-BinlogPath -SnapshotPath $buildResults[$c.Baseline].Path)
        CandidateBinlog = (Get-BinlogPath -SnapshotPath $buildResults[$c.Candidate].Path)
        OutputDir       = $ReportsDir
        MSBuildPath     = $replayHost
        MSBuildCommand  = $replayCommand
        Label           = $c.Label
    }
    if ($c.ExpectMt) { $logArgs['ExpectCandidateMultiThreaded'] = $true }
    if ($DeepLogCompare) { $logArgs['DeepCompare'] = $true }

    & (Join-Path $PSScriptRoot 'Compare-Binlogs.ps1') @logArgs
    $logExit = $LASTEXITCODE

    $outcomes.Add([pscustomobject]@{
            Label         = $c.Label
            ArtifactsPass = ($artifactExit -eq 0)
            LogsPass      = ($logExit -eq 0)
        })
}

# ---------------------------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------------------------

# ---------------------------------------------------------------------------------------------
# Net the -mt differences against the control run.
#
# The control run measures how much this repo's output varies between two identical builds. Any path
# or log line that differs in the -mt comparison *and* also differs in the control has been shown, in
# this very run, to differ without -mt being involved - so it cannot be attributed to -mt. Netting the
# two comparisons this way keeps the verdict strict about -mt while staying robust to pre-existing
# non-determinism that the static rule set has not (yet) seen, for example outputs that only appear on
# a signing-enabled machine.
# ---------------------------------------------------------------------------------------------

function Get-Report {
    param([string] $Name)

    $path = Join-Path $ReportsDir $Name
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    return Get-Content -Raw -LiteralPath $path | ConvertFrom-Json
}

$unexplainedArtifacts = @()
$unexplainedLogLines = @()
$explainedByControl = @()
$netApplied = $false

if (-not $SkipControl) {
    $mtArtifacts = Get-Report -Name 'artifact-compare.mt-vs-baseline.json'
    $controlArtifacts = Get-Report -Name 'artifact-compare.control.json'
    $mtLogs = Get-Report -Name 'log-compare.mt-vs-baseline.json'
    $controlLogs = Get-Report -Name 'log-compare.control.json'

    if ($mtArtifacts -and $controlArtifacts -and $mtLogs -and $controlLogs) {
        $netApplied = $true

        $controlPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($d in @($controlArtifacts.Failures) + @($controlArtifacts.Informational)) {
            if ($d) { [void]$controlPaths.Add($d.Rel) }
        }
        foreach ($d in @($mtArtifacts.Failures)) {
            if (-not $d) { continue }
            if ($controlPaths.Contains($d.Rel)) { $explainedByControl += $d.Rel }
            else { $unexplainedArtifacts += $d.Rel }
        }

        $controlLines = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($section in @('diagnostics', 'functional')) {
            foreach ($d in @($controlLogs.Sections.$section.MissingInCandidate) + @($controlLogs.Sections.$section.ExtraInCandidate)) {
                if ($d) { [void]$controlLines.Add($d.Line) }
            }
        }
        foreach ($section in @('diagnostics', 'functional')) {
            foreach ($d in @($mtLogs.Sections.$section.MissingInCandidate) + @($mtLogs.Sections.$section.ExtraInCandidate)) {
                if (-not $d) { continue }
                if (-not $controlLines.Contains($d.Line)) { $unexplainedLogLines += $d.Line }
            }
        }
    }
}

$md = New-Object System.Collections.Generic.List[string]
$md.Add('# MSBuild -mt build equivalence')
$md.Add('')
$md.Add("Configuration: ``$Configuration``  |  OfficialBuildId: ``$OfficialBuildId``  |  msbuildEngine: ``$(if ($MSBuildEngine) { $MSBuildEngine } else { 'arcade default' })``")
$md.Add('')
$md.Add('| Build | Duration | Snapshot |')
$md.Add('|---|---|---|')
foreach ($run in $runs) {
    $md.Add("| $run | $($buildResults[$run].Duration.ToString('hh\:mm\:ss')) | ``$($buildResults[$run].Path)`` |")
}
$md.Add('')
$md.Add('| Comparison | Artifacts | Logs |')
$md.Add('|---|---|---|')
foreach ($o in $outcomes) {
    $md.Add("| $($o.Label) | $(if ($o.ArtifactsPass) { 'PASS' } else { 'FAIL' }) | $(if ($o.LogsPass) { 'PASS' } else { 'FAIL' }) |")
}
$md.Add('')

if ($netApplied) {
    $md.Add('## Verdict, net of the control run')
    $md.Add('')
    $md.Add("- Artifact differences attributable to ``-mt``: **$($unexplainedArtifacts.Count)**")
    $md.Add("- Artifact differences also present between two identical non-``-mt`` builds: $($explainedByControl.Count)")
    $md.Add("- Log differences attributable to ``-mt``: **$($unexplainedLogLines.Count)**")
    $md.Add('')
    foreach ($p in ($unexplainedArtifacts | Select-Object -First 100)) { $md.Add("- unexplained artifact difference: ``$p``") }
    foreach ($p in ($unexplainedLogLines | Select-Object -First 100)) { $md.Add("- unexplained log difference: ``$p``") }
    if ($unexplainedArtifacts.Count -gt 0 -or $unexplainedLogLines.Count -gt 0) { $md.Add('') }
}

$md.Add('## Investigating a failure')
$md.Add('')
$md.Add('The published report artifact also contains, alongside these reports:')
$md.Add('')
$md.Add('- `binlogs/{baseline,mt,control}.binlog` - the binary log of each build, for tracing a differing file back to the task that wrote it.')
$md.Add('- `evidence/<run>/<path>` - both versions of every differing file, so the bytes can be diffed offline.')
$md.Add('- `evidence-manifest.json` - what was collected, and anything a size cap excluded.')
$md.Add('')
$md.Add('The artifact snapshots themselves are not published (roughly 4.5 GB each).')
$md.Add('')

foreach ($file in (Get-ChildItem -LiteralPath $ReportsDir -Filter '*.md' | Sort-Object Name)) {
    $md.Add((Get-Content -Raw -LiteralPath $file.FullName))
    $md.Add('')
}

$summaryPath = Join-Path $ReportsDir 'summary.md'
($md -join [Environment]::NewLine) | Set-Content -LiteralPath $summaryPath -Encoding utf8

# Machine-readable verdict, also consumed by Collect-Evidence.ps1 to decide what to preserve.
$verdict = [pscustomobject]@{
    GeneratedUtc          = (Get-Date).ToUniversalTime().ToString('o')
    Configuration         = $Configuration
    MSBuildEngine         = $MSBuildEngine
    OfficialBuildId       = $OfficialBuildId
    ControlRunIncluded    = (-not $SkipControl)
    NetOfControlApplied   = $netApplied
    UnexplainedArtifacts  = @($unexplainedArtifacts)
    UnexplainedLogLines   = @($unexplainedLogLines)
    ExplainedByControl    = @($explainedByControl)
    Outcomes              = $outcomes.ToArray()
}
$verdict | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $ReportsDir 'verdict.json') -Encoding utf8

# Preserve what a post-mortem needs. The snapshots themselves are far too large to publish, so this
# keeps the binary logs plus both versions of every differing file. Best-effort: failing to collect
# evidence must never change the verdict.
try {
    & (Join-Path $PSScriptRoot 'Collect-Evidence.ps1') -WorkDir $WorkDir
}
catch {
    Write-Host "##vso[task.logissue type=warning]Evidence collection failed: $($_.Exception.Message)"
    Write-Host "WARNING: evidence collection failed: $($_.Exception.Message)"
}

Write-Host ''
Write-Host '================ SUMMARY ================'
foreach ($o in $outcomes) {
    Write-Host ("{0,-18} artifacts={1,-4} logs={2}" -f $o.Label, $(if ($o.ArtifactsPass) { 'PASS' } else { 'FAIL' }), $(if ($o.LogsPass) { 'PASS' } else { 'FAIL' }))
}
if ($netApplied) {
    Write-Host ("net of control     artifacts={0} unexplained, logs={1} unexplained ({2} artifact diffs explained by the control run)" -f `
            $unexplainedArtifacts.Count, $unexplainedLogLines.Count, $explainedByControl.Count)
}
Write-Host "summary: $summaryPath"
Write-Host ''

# ---------------------------------------------------------------------------------------------
# Verdict
#
# The run FAILS on:
#   1. an unprovable -mt build - a comparison that cannot show -mt was actually on proves nothing;
#   2. any difference attributable to -mt (net of the control run when one is available, or the raw
#      mt-vs-baseline result when -SkipControl was used).
#
# A control run that shows *new* non-determinism of its own is reported as a warning, not a failure:
# it is a real problem worth fixing, but it is not an -mt regression, and failing this pipeline for it
# would train people to ignore a red -mt signal.
# ---------------------------------------------------------------------------------------------

$mtLogReport = Get-Report -Name 'log-compare.mt-vs-baseline.json'
if ($mtLogReport -and $mtLogReport.EvidenceProblems.Count -gt 0) {
    foreach ($p in $mtLogReport.EvidenceProblems) {
        Write-Host "##vso[task.logissue type=error]-mt evidence: $p"
        Write-Host "ERROR: -mt evidence: $p"
    }
    Write-Host 'FAILED: could not prove the -mt build actually ran with -mt, so the comparison proves nothing.'
    exit 1
}

$controlOutcome = $outcomes | Where-Object { $_.Label -eq 'control' }
if ($controlOutcome -and -not ($controlOutcome.ArtifactsPass -and $controlOutcome.LogsPass)) {
    $warning = 'The control run (two identical non-mt builds) differs in a way the rule set does not explain. That is pre-existing build non-determinism, not an -mt regression, but it weakens this check and should be fixed or given a documented rule. See the control report.'
    Write-Host "##vso[task.logissue type=warning]$warning"
    Write-Host "WARNING: $warning"
}

if ($netApplied) {
    if ($unexplainedArtifacts.Count -gt 0 -or $unexplainedLogLines.Count -gt 0) {
        Write-Host "##vso[task.logissue type=error]-mt changed the build output: $($unexplainedArtifacts.Count) artifact difference(s) and $($unexplainedLogLines.Count) log difference(s) that the control run does not explain."
        Write-Host 'FAILED: differences attributable to -mt.'
        exit 1
    }
    Write-Host 'PASSED: no difference attributable to -mt.'
    exit 0
}

$mtOutcome = $outcomes | Where-Object { $_.Label -eq 'mt-vs-baseline' }
if (-not ($mtOutcome.ArtifactsPass -and $mtOutcome.LogsPass)) {
    Write-Host "##vso[task.logissue type=error]-mt changed the build output. No control run was available (-SkipControl), so every unexpected difference is attributed to -mt."
    Write-Host 'FAILED: differences attributable to -mt.'
    exit 1
}
Write-Host 'PASSED: no difference attributable to -mt.'
exit 0
