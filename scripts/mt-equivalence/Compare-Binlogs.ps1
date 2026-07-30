<#
.SYNOPSIS
  Functional comparison of two MSBuild binary logs produced by otherwise-identical builds.

.DESCRIPTION
  Binary logs can never be byte-identical across two runs (they embed wall-clock timestamps,
  durations, node ids and event ordering), so this script compares them *functionally*:

    1. MT evidence  - reads the "Command line arguments" recorded in each binlog and asserts that
                      the candidate build really did run with -mt / --multithreaded and that the
                      baseline did not. Without this the whole comparison would be vacuous.
    2. Structure    - reads the raw event stream out of both binlogs and requires that every target,
                      every (project, target) pair, every task and every project executed exactly
                      the same number of times. This is the strongest of the log checks: it is taken
                      from the events themselves, so unlike the text tiers below it is unaffected by
                      verbosity, by node interleaving, or by the console logger only emitting a
                      target header when the target happens to produce output.
    3. Diagnostics  - replays both logs at quiet verbosity (errors and warnings only) and requires
                      an identical multiset of diagnostics.
    4. Functional   - replays both at normal verbosity, normalizes away scheduling-dependent noise
                      using LogNormalizationRules.json, and requires an identical multiset of lines.
    5. Deep (opt-in)- replays both at diagnostic verbosity and compares the multiset of executed
                      targets and tasks. Superseded by tier 2 for target/task coverage; retained
                      because it also compares which assembly each task was bound to.

.PARAMETER MSBuildPath
  Path to the executable used to replay the binlogs. Must be new enough to read the binlog format
  version written by the build under test - use the bootstrapped MSBuild from the build itself.

.PARAMETER MSBuildCommand
  Sub-command to pass to MSBuildPath ('msbuild' for dotnet.exe, '' for MSBuild.exe).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $BaselineBinlog,
    [Parameter(Mandatory = $true)][string] $CandidateBinlog,
    [Parameter(Mandatory = $true)][string] $OutputDir,
    [Parameter(Mandatory = $true)][string] $MSBuildPath,
    [string] $MSBuildCommand = 'msbuild',
    [string] $Label = 'log',
    [string] $RulesFile,
    [switch] $DeepCompare,
    [switch] $ExpectCandidateMultiThreaded,
    [int]    $MaxReportedLines = 60
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

if (-not $RulesFile) { $RulesFile = Join-Path $PSScriptRoot 'LogNormalizationRules.json' }
foreach ($p in @($BaselineBinlog, $CandidateBinlog, $MSBuildPath, $RulesFile)) {
    if (-not (Test-Path -LiteralPath $p)) { throw "Not found: $p" }
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
# Scratch for the replayed text logs. These run to tens of megabytes each and are fully regenerable
# from the binlogs (which the pipeline publishes), so they are kept out of the report directory and
# deleted at the end rather than shipped in the artifact.
$workDir = Join-Path ([System.IO.Path]::GetTempPath()) "mt-equivalence-logwork-$Label-$PID"
if (Test-Path -LiteralPath $workDir) { Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue }
New-Item -ItemType Directory -Force -Path $workDir | Out-Null

# ---------------------------------------------------------------------------------------------
# MT evidence: the MSBuild command line is recorded near the head of every binlog.
# ---------------------------------------------------------------------------------------------

function Get-BinlogCommandLine {
    param([string] $Path)

    $fs = [System.IO.File]::OpenRead($Path)
    try {
        $gz = [System.IO.Compression.GZipStream]::new($fs, [System.IO.Compression.CompressionMode]::Decompress)
        try {
            # The header, environment block and command line all live in the first few KB.
            $buffer = New-Object byte[] (256 * 1024)
            $read = 0
            $total = 0
            while ($total -lt $buffer.Length) {
                $read = $gz.Read($buffer, $total, $buffer.Length - $total)
                if ($read -le 0) { break }
                $total += $read
            }
            $text = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $total)
        }
        finally { $gz.Dispose() }
    }
    finally { $fs.Dispose() }

    # The record is terminated by a control character, and the value itself may be wrapped in quotes
    # (desktop MSBuild quotes its own exe path because "C:\Program Files\..." contains spaces, which
    # would defeat a "[^"]*" match), so consume printable characters and trim the wrapper afterwards.
    $m = [regex]::Match($text, 'Command line arguments = (?<cmd>[^\x00-\x1F]*)')
    if (-not $m.Success) { return $null }

    $commandLine = $m.Groups['cmd'].Value.Trim()
    if ($commandLine.Length -ge 2 -and $commandLine.StartsWith('"') -and $commandLine.EndsWith('"')) {
        $commandLine = $commandLine.Substring(1, $commandLine.Length - 2)
    }
    return $commandLine
}

function Test-CommandLineMultiThreaded {
    param([string] $CommandLine)

    if (-not $CommandLine) { return $null }
    # Allow a quote as a delimiter: desktop MSBuild command lines quote individual arguments.
    return [bool][regex]::IsMatch($CommandLine, '(?<=^|[\s"])[-/](mt|multithreaded)(?=[\s"]|$)', 'IgnoreCase')
}

$baselineCmd = Get-BinlogCommandLine -Path $BaselineBinlog
$candidateCmd = Get-BinlogCommandLine -Path $CandidateBinlog
$baselineMt = Test-CommandLineMultiThreaded -CommandLine $baselineCmd
$candidateMt = Test-CommandLineMultiThreaded -CommandLine $candidateCmd

Write-Host "[$Label] baseline  command line: $baselineCmd"
Write-Host "[$Label] candidate command line: $candidateCmd"
Write-Host "[$Label] baseline -mt = $baselineMt / candidate -mt = $candidateMt"

$evidenceProblems = New-Object System.Collections.Generic.List[string]
if ($ExpectCandidateMultiThreaded) {
    if ($null -eq $candidateCmd) { $evidenceProblems.Add('Could not read the command line from the candidate binlog.') }
    elseif (-not $candidateMt) { $evidenceProblems.Add("Candidate build did not run with -mt. Command line: $candidateCmd") }
    if ($null -eq $baselineCmd) { $evidenceProblems.Add('Could not read the command line from the baseline binlog.') }
    elseif ($baselineMt) { $evidenceProblems.Add("Baseline build unexpectedly ran with -mt. Command line: $baselineCmd") }
}
else {
    if ($candidateMt -or $baselineMt) { $evidenceProblems.Add('A control comparison must not have -mt on either side.') }
}

# ---------------------------------------------------------------------------------------------
# Replay
# ---------------------------------------------------------------------------------------------

function Invoke-Replay {
    param([string] $Binlog, [string] $Verbosity, [string] $OutFile)

    $flp = "-flp:v=$Verbosity;logfile=$OutFile;ShowTimestamp=false;ShowEventId=false"
    $args = @()
    if ($MSBuildCommand) { $args += $MSBuildCommand }
    $args += @($Binlog, '-noconlog', '-nologo', $flp)

    # A replay of a failed build exits non-zero; that is expected and not an error here.
    & $MSBuildPath @args *> (Join-Path $workDir 'replay-stdout.txt')
    if (-not (Test-Path -LiteralPath $OutFile)) {
        throw "Replay of '$Binlog' at verbosity '$Verbosity' produced no log. See $(Join-Path $workDir 'replay-stdout.txt')."
    }
}

$rules = Get-Content -Raw -LiteralPath $RulesFile | ConvertFrom-Json
Add-Type -TypeDefinition (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'MtCompareNative.cs')) -ErrorAction Stop

$opts = [System.Text.RegularExpressions.RegexOptions]::Compiled
$prefixRegexes = [System.Text.RegularExpressions.Regex[]]@($rules.prefix | ForEach-Object { [regex]::new($_.pattern, $opts) })
$dropRegexes = [System.Text.RegularExpressions.Regex[]]@($rules.drop | ForEach-Object { [regex]::new($_.pattern, $opts) })
$replaceFrom = [System.Text.RegularExpressions.Regex[]]@($rules.replace | ForEach-Object { [regex]::new($_.pattern, $opts) })
$replaceTo = [string[]]@($rules.replace | ForEach-Object { $_.replacement })
$setOnlyRegexes = [System.Text.RegularExpressions.Regex[]]@($rules.setOnly | ForEach-Object { [regex]::new($_.pattern, $opts) })
$knownMtOnly = @($rules.knownMtOnly | ForEach-Object { [pscustomobject]@{ Regex = [regex]::new($_.pattern, $opts); Reason = $_.reason } })

# Extractors for the deep (diagnostic verbosity) coverage comparison. The *set* of targets, tasks and
# task->assembly bindings must match; their counts and ordering are scheduling artifacts.
$deepExtractorNames = [string[]]@('targets', 'tasks', 'taskAssemblies')
# Target *headers* in a text log are emitted only when a target produces output in a contiguous block,
# so which target names appear is partly a logging/interleaving artifact - the non-mt control run shows
# the same instability. Target coverage is therefore reported but not treated as a failure; the task and
# task->assembly sets are stable in the control and are enforced.
$deepFailingExtractors = [string[]]@('tasks', 'taskAssemblies')
$deepPrefix = '^(?:\d{1,2}:\d{2}:\d{2}\.\d{3}\s+)?(?:\d+(?::\d+)?>)?\s*'
$deepExtractors = [System.Text.RegularExpressions.Regex[]]@(
    [regex]::new($deepPrefix + '(?<v>[A-Za-z_][A-Za-z0-9_.]*):\s*\(TargetId:\d+\)$', $opts)
    [regex]::new($deepPrefix + 'Task "(?<v>[^"]+)"\s*\(TaskId:\d+\)$', $opts)
    [regex]::new($deepPrefix + 'Using "(?<v>[^"]+" task from assembly "[^"]+)"\.$', $opts)
)

function Get-NormalizedLineCounts {
    param([string] $Path)

    $seen = [System.Collections.Generic.HashSet[string]]::new()
    return [MtCompareNative]::NormalizedLineCounts($Path, $prefixRegexes, $dropRegexes, $replaceFrom, $replaceTo, $setOnlyRegexes, $seen)
}

$sections = [ordered]@{}
$knownMtDifferences = New-Object System.Collections.Generic.List[object]

# ---------------------------------------------------------------------------------------------
# Structural comparison, taken from the binlog event stream rather than from replayed text.
#
# The text tiers below have to tolerate a lot of noise, and two of the normalization rules
# (the target-header setOnly rule and its knownMtOnly counterpart) deliberately stop comparing
# how many times a target header appears. That is only safe if something else pins down what
# actually ran, which is what this does: exact execution counts for every target, every
# (project, target) pair, every task and every project, straight from the events.
# ---------------------------------------------------------------------------------------------

function Get-BinlogReaderDirectory {
    # Any recent Microsoft.Build can read the binlog; it just has to be the .NET (not .NET Framework)
    # build, because this script runs under pwsh. Arcade installs one into <repo>\.dotnet whenever
    # global.json has a 'tools.dotnet' entry, which is independent of -msbuildEngine.
    $candidates = New-Object System.Collections.Generic.List[string]
    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    foreach ($root in @(
            (Join-Path $repoRoot '.dotnet'),
            $env:DOTNET_INSTALL_DIR,
            $env:DOTNET_ROOT,
            (Split-Path -Parent ((Get-Command dotnet -ErrorAction SilentlyContinue)).Source))) {

        if (-not $root) { continue }
        $sdkDir = Join-Path $root 'sdk'
        if (-not (Test-Path -LiteralPath $sdkDir)) { continue }
        foreach ($d in (Get-ChildItem -LiteralPath $sdkDir -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
            if (Test-Path -LiteralPath (Join-Path $d.FullName 'Microsoft.Build.dll')) { $candidates.Add($d.FullName) }
        }
    }
    return $candidates
}

$structureReaderError = $null
$structureReady = $false
foreach ($dir in (Get-BinlogReaderDirectory)) {
    try {
        # LoadFrom also registers the assembly's directory as a probing path, so Microsoft.Build's
        # own dependencies resolve without any custom AssemblyLoadContext plumbing.
        [void][System.Reflection.Assembly]::LoadFrom((Join-Path $dir 'Microsoft.Build.Framework.dll'))
        [void][System.Reflection.Assembly]::LoadFrom((Join-Path $dir 'Microsoft.Build.dll'))
        Add-Type -TypeDefinition (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'BinlogStructure.cs')) -ReferencedAssemblies @(
            (Join-Path $dir 'Microsoft.Build.dll'),
            (Join-Path $dir 'Microsoft.Build.Framework.dll'),
            'System.Collections',
            'System.Runtime') -ErrorAction Stop
        Write-Host "[$Label] structural comparison using the MSBuild assemblies in $dir"
        $structureReady = $true
        break
    }
    catch {
        $structureReaderError = $_.Exception.Message
    }
}

if (-not $structureReady) {
    # This is the control that lets the text tiers ignore target-header counts, so not being able to
    # run it has to be loud rather than silent.
    $evidenceProblems.Add("Could not load an MSBuild assembly able to read the binlog event stream, so the structural comparison did not run. Last error: $structureReaderError")
}

function Compare-LineCounts {
    param($Baseline, $Candidate)

    $onlyBaseline = New-Object System.Collections.Generic.List[object]
    $onlyCandidate = New-Object System.Collections.Generic.List[object]

    foreach ($kv in $Baseline.GetEnumerator()) {
        $candCount = 0
        [void]$Candidate.TryGetValue($kv.Key, [ref] $candCount)
        if ($candCount -lt $kv.Value) {
            $onlyBaseline.Add([pscustomobject]@{ Line = $kv.Key; Baseline = $kv.Value; Candidate = $candCount })
        }
    }
    foreach ($kv in $Candidate.GetEnumerator()) {
        $baseCount = 0
        [void]$Baseline.TryGetValue($kv.Key, [ref] $baseCount)
        if ($baseCount -lt $kv.Value) {
            $onlyCandidate.Add([pscustomobject]@{ Line = $kv.Key; Baseline = $baseCount; Candidate = $kv.Value })
        }
    }

    [pscustomobject]@{
        BaselineDistinct  = $Baseline.Count
        CandidateDistinct = $Candidate.Count
        BaselineTotal     = ($Baseline.Values | Measure-Object -Sum).Sum
        CandidateTotal    = ($Candidate.Values | Measure-Object -Sum).Sum
        MissingInCandidate = @($onlyBaseline | Sort-Object Line)
        ExtraInCandidate   = @($onlyCandidate | Sort-Object Line)
    }
}

function Split-KnownMtDifferences {
    param($Comparison)

    if (-not $ExpectCandidateMultiThreaded -or $knownMtOnly.Count -eq 0) { return $Comparison }

    $remaining = New-Object System.Collections.Generic.List[object]
    foreach ($d in $Comparison.ExtraInCandidate) {
        $matched = $null
        foreach ($k in $knownMtOnly) {
            if ($k.Regex.IsMatch([string]$d.Line)) { $matched = $k; break }
        }
        if ($matched) {
            $knownMtDifferences.Add([pscustomobject]@{ Line = $d.Line; Baseline = $d.Baseline; Candidate = $d.Candidate; Reason = $matched.Reason })
        }
        else {
            $remaining.Add($d)
        }
    }

    return [pscustomobject]@{
        BaselineDistinct   = $Comparison.BaselineDistinct
        CandidateDistinct  = $Comparison.CandidateDistinct
        BaselineTotal      = $Comparison.BaselineTotal
        CandidateTotal     = $Comparison.CandidateTotal
        MissingInCandidate = @($Comparison.MissingInCandidate)
        ExtraInCandidate   = $remaining.ToArray()
    }
}

if ($structureReady) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $baseStruct = [BinlogStructure]::Collect((Resolve-Path -LiteralPath $BaselineBinlog).Path)
    $candStruct = [BinlogStructure]::Collect((Resolve-Path -LiteralPath $CandidateBinlog).Path)
    $sw.Stop()

    foreach ($dim in @('Targets', 'TargetsByProject', 'Tasks', 'Projects', 'Diagnostics')) {
        $name = 'structure/' + $dim.Substring(0, 1).ToLowerInvariant() + $dim.Substring(1)
        # Deliberately not passed through Split-KnownMtDifferences: this tier exists precisely to be
        # the check that nothing is excused, so a known bug must not be able to suppress it either.
        $cmp = Compare-LineCounts -Baseline $baseStruct.$dim -Candidate $candStruct.$dim
        $sections[$name] = $cmp
        Write-Host ("[{0}] {1}: {2} distinct / {3} executions baseline, {4} / {5} candidate; {6} missing, {7} extra" -f `
                $Label, $name, $cmp.BaselineDistinct, $cmp.BaselineTotal, $cmp.CandidateDistinct, $cmp.CandidateTotal, `
                $cmp.MissingInCandidate.Count, $cmp.ExtraInCandidate.Count)
    }
    Write-Host ("[{0}] structural extraction took {1:n1}s" -f $Label, $sw.Elapsed.TotalSeconds)
}

foreach ($tier in @(
        @{ Name = 'diagnostics'; Verbosity = 'q'; Enabled = $true },
        @{ Name = 'functional'; Verbosity = 'n'; Enabled = $true })) {

    if (-not $tier.Enabled) { continue }

    $v = $tier.Verbosity
    $baseLog = Join-Path $workDir "baseline.$v.log"
    $candLog = Join-Path $workDir "candidate.$v.log"

    Write-Host "[$Label] replaying at verbosity '$v'..."
    Invoke-Replay -Binlog $BaselineBinlog -Verbosity $v -OutFile $baseLog
    Invoke-Replay -Binlog $CandidateBinlog -Verbosity $v -OutFile $candLog

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $baseCounts = Get-NormalizedLineCounts -Path $baseLog
    $candCounts = Get-NormalizedLineCounts -Path $candLog
    $sw.Stop()

    $cmp = Split-KnownMtDifferences -Comparison (Compare-LineCounts -Baseline $baseCounts -Candidate $candCounts)
    $sections[$tier.Name] = $cmp
    Write-Host ("[{0}] {1}: {2} distinct baseline lines / {3} candidate; {4} missing, {5} extra (normalized in {6:n1}s)" -f `
            $Label, $tier.Name, $cmp.BaselineDistinct, $cmp.CandidateDistinct, $cmp.MissingInCandidate.Count, $cmp.ExtraInCandidate.Count, $sw.Elapsed.TotalSeconds)
}

# ---------------------------------------------------------------------------------------------
# Deep coverage comparison: which targets ran, which tasks ran, and which assembly each task was
# bound to. Compared as sets, from a diagnostic-verbosity replay.
# ---------------------------------------------------------------------------------------------

$coverage = @{}
$coverageNames = New-Object System.Collections.Generic.List[string]
if ($DeepCompare) {
    $baseLog = Join-Path $workDir 'baseline.diag.log'
    $candLog = Join-Path $workDir 'candidate.diag.log'
    Write-Host "[$Label] replaying at verbosity 'diag' for target/task coverage..."
    Invoke-Replay -Binlog $BaselineBinlog -Verbosity 'diag' -OutFile $baseLog
    Invoke-Replay -Binlog $CandidateBinlog -Verbosity 'diag' -OutFile $candLog

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $baseSets = [MtCompareNative]::ExtractSets($baseLog, $deepExtractorNames, $deepExtractors)
    $candSets = [MtCompareNative]::ExtractSets($candLog, $deepExtractorNames, $deepExtractors)
    $sw.Stop()
    Remove-Item -LiteralPath $baseLog, $candLog -Force -ErrorAction SilentlyContinue

    foreach ($name in $deepExtractorNames) {
        $missing = @($baseSets[$name] | Where-Object { -not $candSets[$name].Contains($_) } | Sort-Object)
        $extra = @($candSets[$name] | Where-Object { -not $baseSets[$name].Contains($_) } | Sort-Object)
        $coverageNames.Add($name)
        $coverage[$name] = [pscustomobject]@{
            BaselineCount      = $baseSets[$name].Count
            CandidateCount     = $candSets[$name].Count
            MissingInCandidate = $missing
            ExtraInCandidate   = $extra
        }
        Write-Host ("[{0}] coverage/{1}: baseline {2}, candidate {3}; {4} missing, {5} extra" -f `
                $Label, $name, $baseSets[$name].Count, $candSets[$name].Count, $missing.Count, $extra.Count)
    }
    Write-Host ("[{0}] coverage extraction took {1:n1}s" -f $Label, $sw.Elapsed.TotalSeconds)
}

# ---------------------------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------------------------

$diagnosticsMismatch = $sections['diagnostics'].MissingInCandidate.Count + $sections['diagnostics'].ExtraInCandidate.Count
$functionalMismatch = $sections['functional'].MissingInCandidate.Count + $sections['functional'].ExtraInCandidate.Count
$structureMismatch = 0
$structureSections = @($sections.Keys | Where-Object { $_ -like 'structure/*' })
foreach ($name in $structureSections) {
    $structureMismatch += $sections[$name].MissingInCandidate.Count + $sections[$name].ExtraInCandidate.Count
}
$coverageMismatch = 0
foreach ($name in $coverageNames) {
    if ($name -notin $deepFailingExtractors) { continue }
    $coverageMismatch += $coverage[$name].MissingInCandidate.Count + $coverage[$name].ExtraInCandidate.Count
}

$passed = ($evidenceProblems.Count -eq 0) -and ($diagnosticsMismatch -eq 0) -and ($functionalMismatch -eq 0) -and ($structureMismatch -eq 0) -and ($coverageMismatch -eq 0)

$report = [pscustomobject]@{
    Label                  = $Label
    BaselineBinlog         = (Resolve-Path $BaselineBinlog).Path
    CandidateBinlog        = (Resolve-Path $CandidateBinlog).Path
    BaselineCommandLine    = $baselineCmd
    CandidateCommandLine   = $candidateCmd
    BaselineMultiThreaded  = $baselineMt
    CandidateMultiThreaded = $candidateMt
    EvidenceProblems       = @($evidenceProblems)
    Sections               = $sections
    Coverage               = $coverage
    KnownMtDifferences     = $knownMtDifferences.ToArray()
    Passed                 = $passed
    GeneratedUtc           = (Get-Date).ToUniversalTime().ToString('o')
}

$jsonPath = Join-Path $OutputDir "log-compare.$Label.json"
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $jsonPath -Encoding utf8

$knownMtLineTotal = 0
foreach ($k in $knownMtDifferences) { $knownMtLineTotal += $k.Candidate }

$md = New-Object System.Collections.Generic.List[string]
$md.Add("## Log comparison: ``$Label``")
$md.Add('')
$md.Add('| | |')
$md.Add('|---|---|')
$md.Add("| Baseline binlog | ``$($report.BaselineBinlog)`` |")
$md.Add("| Candidate binlog | ``$($report.CandidateBinlog)`` |")
$md.Add("| Baseline ran with -mt | $baselineMt |")
$md.Add("| Candidate ran with -mt | $candidateMt |")
$md.Add("| Diagnostics (errors/warnings) differences | $diagnosticsMismatch |")
$md.Add("| Functional (normal verbosity) differences | $functionalMismatch |")
if ($structureSections.Count -gt 0) {
    $md.Add("| Structural differences (target/task/project execution counts) | $structureMismatch |")
}
if ($coverageNames.Count -gt 0) { $md.Add("| Target/task coverage differences | $coverageMismatch |") }
$md.Add("| Known, already-filed -mt differences | $($knownMtDifferences.Count) distinct / $knownMtLineTotal lines |")
$md.Add("| Result | $(if ($passed) { 'PASS' } else { 'FAIL' }) |")
$md.Add('')

if ($evidenceProblems.Count -gt 0) {
    $md.Add('### -mt evidence problems')
    $md.Add('')
    foreach ($p in $evidenceProblems) { $md.Add("- $p") }
    $md.Add('')
}

foreach ($name in $sections.Keys) {
    $s = $sections[$name]
    if (($s.MissingInCandidate.Count + $s.ExtraInCandidate.Count) -eq 0) { continue }
    $md.Add("### $name differences")
    $md.Add('')
    if ($s.MissingInCandidate.Count -gt 0) {
        $md.Add("Present in baseline but missing (or fewer) in the candidate ($($s.MissingInCandidate.Count)):")
        $md.Add('')
        foreach ($d in ($s.MissingInCandidate | Select-Object -First $MaxReportedLines)) {
            $md.Add("- ``$($d.Line)`` (baseline x$($d.Baseline), candidate x$($d.Candidate))")
        }
        $md.Add('')
    }
    if ($s.ExtraInCandidate.Count -gt 0) {
        $md.Add("Present in the candidate but missing (or fewer) in baseline ($($s.ExtraInCandidate.Count)):")
        $md.Add('')
        foreach ($d in ($s.ExtraInCandidate | Select-Object -First $MaxReportedLines)) {
            $md.Add("- ``$($d.Line)`` (baseline x$($d.Baseline), candidate x$($d.Candidate))")
        }
        $md.Add('')
    }
}

foreach ($name in $coverageNames) {
    $c = $coverage[$name]
    if (($c.MissingInCandidate.Count + $c.ExtraInCandidate.Count) -eq 0) { continue }
    $enforced = $name -in $deepFailingExtractors
    $md.Add("### coverage/$name differences$(if (-not $enforced) { ' (informational)' })")
    $md.Add('')
    if (-not $enforced) {
        $md.Add('_Target headers are emitted by the text logger only when a target produces output in a contiguous block, so the set of names is partly an interleaving artifact. The non-mt control run shows the same instability, so this signal is reported but not enforced._')
        $md.Add('')
    }
    foreach ($d in ($c.MissingInCandidate | Select-Object -First $MaxReportedLines)) { $md.Add("- only in baseline: ``$d``") }
    foreach ($d in ($c.ExtraInCandidate | Select-Object -First $MaxReportedLines)) { $md.Add("- only in candidate: ``$d``") }
    $md.Add('')
}

if ($knownMtDifferences.Count -gt 0) {
    $md.Add('### Known -mt-only log differences (already filed; not treated as failures)')
    $md.Add('')
    foreach ($d in ($knownMtDifferences | Sort-Object -Property @{Expression = { -$_.Candidate } } | Select-Object -First $MaxReportedLines)) {
        $md.Add("- x$($d.Candidate) ``$($d.Line)``")
        $md.Add("  - $($d.Reason)")
    }
    $md.Add('')
}

$mdPath = Join-Path $OutputDir "log-compare.$Label.md"
($md -join [Environment]::NewLine) | Set-Content -LiteralPath $mdPath -Encoding utf8

Write-Host "[$Label] reports: $jsonPath"
Write-Host "[$Label]          $mdPath"

# Drop the replayed text logs: they are large and regenerable from the binlogs.
Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue

if (-not $passed) {
    Write-Host "[$Label] FAIL"
    foreach ($p in $evidenceProblems) { Write-Host "  evidence: $p" }
    exit 1
}
exit 0

