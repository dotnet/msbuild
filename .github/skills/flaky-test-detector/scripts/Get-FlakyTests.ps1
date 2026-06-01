<#
.SYNOPSIS
    Detects flaky tests by scanning recent msbuild CI builds (PR validation + rolling builds
    on main) for test failures that recur across multiple independent evidence sources.

.DESCRIPTION
    Queries the Azure DevOps builds API (org `dnceng-public`, project `public`, pipeline
    `msbuild-pr` = definition 75) for recent *failed* builds, downloads the relevant
    "... test logs" pipeline artifacts, parses the TRX results for failed tests, and
    aggregates failures by test name across distinct *evidence sources*.

    An evidence source is one of:
      * a Pull Request (all failed PR-validation builds for that PR collapse to one source), or
      * a single failed rolling/CI build on `main` (reason individualCI/batchedCI/schedule).

    Both signals indicate flakiness rather than a real regression:
      * the same test failing across several *unrelated, approved* PRs cannot be caused by any
        one PR's changes;
      * a test failing on rolling `main` builds is suspicious because `main` is expected green.

    SCOPE (current):
      * Branch: `main` only. PR builds must target `main`; rolling builds must be on
        `refs/heads/main`. Builds on vs18.x / exp/ branches are ignored.
      * PR builds are only counted when the PR is **non-draft AND approved (or merged)**, so the
        signal isn't polluted by legitimate failures in in-development work.
      * Rolling builds need no approval filter (they have no PR).

    Everything this script touches on Azure DevOps is anonymously accessible:
      * build list API           (/_apis/build/builds?...)
      * build timeline API       (/_apis/build/builds/{id}/timeline) -- used to find failed legs
      * build artifacts list API (/_apis/build/builds/{id}/artifacts)
      * artifact zip download    (...artifacts?artifactName=...&$format=zip)
    The Test Management API (/_apis/test/...) and the vstmr testresults host are NOT
    anonymously accessible on dnceng-public, so this script relies on the published
    "test logs" artifacts (which contain the .trx files) instead.

    Note: TRX files contain the failed test name plus the error message/stack trace, but
    NOT captured stdout. For full console output, see the "... build logs" artifacts (their
    name is misleading; they contain the per-assembly xUnit console .log files and a
    .binlog). This script does not download "build logs" -- the fixer workflow pulls those
    on demand when it needs deeper diagnostics.

.PARAMETER MaxBuilds
    Maximum number of failed builds (all reasons) to consider. Default: 60.

.PARAMETER MinSources
    Minimum number of distinct evidence sources (PRs + rolling builds) a test must fail in to
    be flagged. Default: 3.

.PARAMETER DaysBack
    Only consider builds from within this many days. Default: 14.

.PARAMETER MaxArtifactDownloads
    Safety guard on the total number of artifacts downloaded in one run. If this guard trips,
    the scan is marked incomplete (`scanComplete: false`) so callers can refuse to act on
    biased/partial data. Default: 150.

.PARAMETER DefinitionId
    Azure DevOps pipeline definition ID for msbuild-pr. Default: 75.

.PARAMETER TargetBranch
    The branch to scope to (PR base branch and rolling-build source branch). Default: 'main'.

.PARAMETER Org
    Azure DevOps organization. Default: 'dnceng-public'.

.PARAMETER Project
    Azure DevOps project. Default: 'public'.

.PARAMETER Repo
    GitHub repository (owner/name) used for PR metadata and existing-issue lookups. Default: 'dotnet/msbuild'.

.PARAMETER AllLegs
    Download every "test logs" artifact for each failed build instead of only the legs the
    timeline reports as failed. Slower but exhaustive.

.PARAMETER NoApprovalFilter
    Bypass the non-draft/approved filter on PR sources (useful for local smoke testing where
    few PRs are approved). Rolling-build sources are unaffected by this switch.

.PARAMETER JsonOut
    Optional path. When set, the structured report object is written to this file as JSON.

.EXAMPLE
    ./Get-FlakyTests.ps1
    ./Get-FlakyTests.ps1 -MinSources 2 -DaysBack 7 -NoApprovalFilter
    ./Get-FlakyTests.ps1 -MaxBuilds 100 -DaysBack 30 -JsonOut flaky.json
#>

[CmdletBinding()]
param(
    [int]$MaxBuilds = 60,
    [int]$MinSources = 3,
    [int]$DaysBack = 14,
    [int]$MaxArtifactDownloads = 150,
    [int]$DefinitionId = 75,
    [string]$TargetBranch = "main",
    [string]$Org = "dnceng-public",
    [string]$Project = "public",
    [string]$Repo = "dotnet/msbuild",
    [switch]$AllLegs,
    [switch]$NoApprovalFilter,
    [string]$JsonOut
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"   # large speedup for Invoke-WebRequest

$baseUrl = "https://dev.azure.com/$Org/$Project"
$trxNs = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"
$targetRef = "refs/heads/$TargetBranch"
$rollingReasons = @('individualCI', 'batchedCI', 'schedule')

# Hardening limits for untrusted PR-produced artifacts.
$MaxZipBytes = 200MB
$MaxTrxBytes = 60MB
$MaxTrxPerArtifact = 200

# Human-readable progress goes to the host/information log stream; the machine-readable
# JSON result is the only thing written to stdout (via Write-Output), keeping it clean.
function Write-Log {
    param([string]$Message, [string]$Color = "Gray")
    Write-Host $Message -ForegroundColor $Color
}

function Get-ShortHash {
    param([string]$Text)
    if ([string]::IsNullOrWhiteSpace($Text)) { return "" }
    $normalized = ($Text -replace '\s+', ' ').Trim()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = $sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($normalized))
        return ([System.BitConverter]::ToString($bytes) -replace '-', '').Substring(0, 8).ToLowerInvariant()
    }
    finally { $sha.Dispose() }
}

# Tokenize a leg/artifact label into a comparable lowercase token set (split on
# non-alphanumerics and camelCase boundaries; drop generic words).
function Get-LegTokens {
    param([string]$Label)
    $stop = @('on', 'test', 'tests', 'logs', 'log', 'no', 'bootstrap', 'the', 'and', 'build')
    $spaced = [regex]::Replace($Label, '(?<=[a-z0-9])(?=[A-Z])', ' ')
    $tokens = ($spaced -split '[^A-Za-z0-9]+') | ForEach-Object { $_.ToLowerInvariant() } |
        Where-Object { $_.Length -ge 2 -and ($stop -notcontains $_) }
    return @($tokens | Select-Object -Unique)
}

# Parse failed tests out of a single TRX file using a hardened XmlReader.
function Read-TrxFailures {
    param([string]$Path)
    $results = @()
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.IgnoreComments = $true
    $settings.IgnoreWhitespace = $true
    $doc = New-Object System.Xml.XmlDocument
    $doc.XmlResolver = $null
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $reader = [System.Xml.XmlReader]::Create($stream, $settings)
        try { $doc.Load($reader) } finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }

    $nsm = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $nsm.AddNamespace('t', $trxNs)
    foreach ($node in $doc.SelectNodes('//t:UnitTestResult[@outcome="Failed"]', $nsm)) {
        $fullName = $node.testName
        if (-not $fullName) { continue }
        $msgNode = $node.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $nsm)
        $message = if ($msgNode) { $msgNode.InnerText } else { "" }
        $results += [PSCustomObject]@{ FullName = $fullName; Message = $message }
    }
    return $results
}

function Get-BuildDate {
    param($Build)
    if ($Build.startTime -is [datetime]) { return $Build.startTime.ToString("yyyy-MM-dd") }
    if ($Build.startTime) {
        $s = [string]$Build.startTime
        if ($s.Length -ge 10) { return $s.Substring(0, 10) }
        return $s
    }
    return ""
}

# ---------------------------------------------------------------------------
# Step 1: Query AzDo for recent failed builds (all reasons), then partition.
# ---------------------------------------------------------------------------
$minTime = (Get-Date).ToUniversalTime().AddDays(-$DaysBack).ToString("yyyy-MM-ddTHH:mm:ssZ")
Write-Log "`n=== Step 1: Fetching failed msbuild builds (last $DaysBack days, up to $MaxBuilds) ===" "Cyan"

$buildsUrl = "$baseUrl/_apis/build/builds?definitions=$DefinitionId&resultFilter=failed&minTime=$minTime&`$top=$MaxBuilds&api-version=7.0"
try {
    $buildsResponse = Invoke-RestMethod -Uri $buildsUrl -Method Get -ContentType "application/json"
}
catch {
    Write-Error "Failed to query AzDo builds API: $_"
    exit 1
}

$builds = @($buildsResponse.value)
# A scan is complete only if we were not truncated by the $top window. The AzDo build-list API
# does not expose a reliable total-count field, so treat a full page ($MaxBuilds results) as
# potentially truncated.
$scanComplete = $builds.Count -lt $MaxBuilds

function Write-EmptyReport {
    param([bool]$Complete = $true)
    $empty = [PSCustomObject]@{
        scanComplete = $Complete; generatedAt = (Get-Date).ToUniversalTime().ToString("o")
        definitionId = $DefinitionId; targetBranch = $TargetBranch; daysBack = $DaysBack
        minSources = $MinSources; buildsScanned = 0; prSources = 0; rollingSources = 0; flakyTests = @()
    }
    $json = $empty | ConvertTo-Json -Depth 8
    if ($JsonOut) { $json | Set-Content -Path $JsonOut }
    $json
}

if ($builds.Count -eq 0) {
    Write-Log "No failed builds found in the last $DaysBack days." "Green"
    Write-EmptyReport -Complete $true
    return
}
Write-Log "Found $($builds.Count) failed build(s)" "Green"

$prBuilds = @($builds | Where-Object { $_.reason -eq 'pullRequest' })
$rollingBuilds = @($builds | Where-Object { ($rollingReasons -contains $_.reason) -and ($_.sourceBranch -eq $targetRef) })
Write-Log "  $($prBuilds.Count) PR-validation build(s), $($rollingBuilds.Count) rolling build(s) on $TargetBranch" "Green"

# Group PR builds by PR number.
$prByNum = @{}
foreach ($build in $prBuilds) {
    $prNum = $build.triggerInfo.'pr.number'
    if (-not $prNum) { continue }
    if (-not $prByNum.ContainsKey($prNum)) { $prByNum[$prNum] = @() }
    $prByNum[$prNum] += $build
}

# ---------------------------------------------------------------------------
# Step 1b: Keep only PR sources that target $TargetBranch and are non-draft +
# approved (or merged). A failure in an approved/merged PR is far more likely
# flaky than a regression introduced by that PR, since reviewers signed off.
# ---------------------------------------------------------------------------
$skippedPRs = @()
$keptPRs = @{}
$ghAvailable = [bool](Get-Command gh -ErrorAction SilentlyContinue)
if ($prByNum.Count -gt 0) {
    Write-Log "`n=== Step 1b: Filtering PR sources (base=$TargetBranch, non-draft, approved/merged) ===" "Cyan"
    if (-not $ghAvailable) {
        Write-Log "WARNING: 'gh' not available; cannot verify PR base/approval." "Red"
        if ($NoApprovalFilter) {
            Write-Log "Including all PR sources unfiltered (-NoApprovalFilter)." "DarkYellow"
            $keptPRs = $prByNum
        }
        else {
            Write-Log "Dropping ALL PR sources (use -NoApprovalFilter to include them)." "DarkYellow"
        }
    }
    else {
        $savedPager = $env:GH_PAGER; $env:GH_PAGER = ""
        foreach ($prNum in @($prByNum.Keys)) {
            $meta = $null
            try {
                $raw = gh pr view $prNum --repo $Repo --json isDraft,reviewDecision,state,baseRefName 2>$null
                if ($raw) { $meta = $raw | ConvertFrom-Json }
            }
            catch { }
            if ($null -eq $meta) {
                $skippedPRs += [PSCustomObject]@{ PR = [int]$prNum; Reason = "metadata-unavailable" }
                continue
            }
            if ($meta.baseRefName -ne $TargetBranch) {
                $skippedPRs += [PSCustomObject]@{ PR = [int]$prNum; Reason = "base=$($meta.baseRefName)" }
                continue
            }
            if (-not $NoApprovalFilter) {
                $approved = ($meta.reviewDecision -eq 'APPROVED') -or ($meta.state -eq 'MERGED')
                if ($meta.isDraft) {
                    $skippedPRs += [PSCustomObject]@{ PR = [int]$prNum; Reason = "draft" }
                    continue
                }
                if (-not $approved) {
                    $skippedPRs += [PSCustomObject]@{ PR = [int]$prNum; Reason = "not-approved ($($meta.reviewDecision))" }
                    continue
                }
            }
            $keptPRs[$prNum] = $prByNum[$prNum]
        }
        $env:GH_PAGER = $savedPager
    }
    Write-Log "Kept $($keptPRs.Count) PR source(s); skipped $($skippedPRs.Count)" "Green"
}

# Build the unified ordered list of evidence sources.
$sources = @()
foreach ($prNum in ($keptPRs.Keys | Sort-Object)) {
    $sources += [PSCustomObject]@{
        Key = "PR#$prNum"; Type = 'pr'; Pr = [int]$prNum; Display = "PR #$prNum"; Builds = @($keptPRs[$prNum])
    }
}
foreach ($build in ($rollingBuilds | Sort-Object id)) {
    $sources += [PSCustomObject]@{
        Key = "CI#$($build.id)"; Type = 'rolling'; Pr = $null; Display = "rolling build $($build.id)"; Builds = @($build)
    }
}

if ($sources.Count -eq 0) {
    Write-Log "No eligible evidence sources after filtering." "Green"
    Write-EmptyReport -Complete $scanComplete
    return
}
Write-Log "Total evidence sources: $($sources.Count) ($($keptPRs.Count) PR, $($rollingBuilds.Count) rolling)" "Green"

# ---------------------------------------------------------------------------
# Step 2: For each source build, download relevant "test logs" artifacts and
# parse TRX for failed tests.
# ---------------------------------------------------------------------------
Write-Log "`n=== Step 2: Scanning sources for failed tests (via test-logs artifacts) ===" "Cyan"

# normalizedTestName -> list of failure records
$testFailures = @{}
$downloads = 0
$scannedBuilds = 0
$tmpRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("flaky-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tmpRoot | Out-Null

try {
    :sourceLoop foreach ($source in $sources) {
        foreach ($build in $source.Builds) {
            $buildId = $build.id
            $buildUrl = "$baseUrl/_build/results?buildId=$buildId"
            $buildDate = Get-BuildDate $build
            Write-Log "$($source.Display): build $buildId ($buildDate)..." "White"

            # Determine failed legs from the timeline so we only pull relevant artifacts.
            $failedLegTokenSets = @()
            if (-not $AllLegs) {
                try {
                    $timeline = Invoke-RestMethod -Uri "$baseUrl/_apis/build/builds/$buildId/timeline?api-version=7.1"
                    $failedJobs = @($timeline.records | Where-Object { $_.type -eq 'Job' -and $_.result -eq 'failed' })
                    foreach ($job in $failedJobs) { $failedLegTokenSets += , (Get-LegTokens $job.name) }
                }
                catch { }
            }

            # List artifacts (anonymous).
            try {
                $artifacts = @((Invoke-RestMethod -Uri "$baseUrl/_apis/build/builds/$buildId/artifacts?api-version=7.0").value)
            }
            catch {
                Write-Log "  [could not list artifacts: $($_.Exception.Message)]" "DarkYellow"
                $scannedBuilds++
                continue
            }

            $testLogArtifacts = @($artifacts | Where-Object { $_.name -like "*test logs*" })
            if ($testLogArtifacts.Count -eq 0) {
                Write-Log "  [no 'test logs' artifacts]" "DarkGray"
                $scannedBuilds++
                continue
            }

            # If we have failed-leg info, keep only matching artifacts (artifact tokens subset
            # of some failed leg's tokens). If nothing matches, fall back to all artifacts so we
            # never silently miss data.
            $selected = $testLogArtifacts
            if (-not $AllLegs -and $failedLegTokenSets.Count -gt 0) {
                $matched = @($testLogArtifacts | Where-Object {
                    $aTokens = Get-LegTokens ($_.name -replace '(?i)\s*test logs\s*$', '')
                    if ($aTokens.Count -eq 0) { return $false }
                    foreach ($legTokens in $failedLegTokenSets) {
                        $isSubset = @($aTokens | Where-Object { $legTokens -notcontains $_ }).Count -eq 0
                        if ($isSubset) { return $true }
                    }
                    return $false
                })
                if ($matched.Count -gt 0) { $selected = $matched }
            }

            foreach ($artifact in $selected) {
                if ($downloads -ge $MaxArtifactDownloads) {
                    Write-Log "  [reached MaxArtifactDownloads ($MaxArtifactDownloads); marking scan incomplete]" "DarkYellow"
                    $scanComplete = $false
                    break sourceLoop
                }
                $downloads++

                $leg = ($artifact.name -replace '(?i)\s*test logs\s*$', '').Trim()
                $zipPath = Join-Path $tmpRoot ("b${buildId}-" + ($artifact.name -replace '[^\w]', '_') + ".zip")
                $extractDir = $zipPath -replace '\.zip$', ''
                $dlUrl = "$baseUrl/_apis/build/builds/$buildId/artifacts?artifactName=$([uri]::EscapeDataString($artifact.name))&api-version=7.0&`$format=zip"
                try {
                    Invoke-WebRequest -Uri $dlUrl -OutFile $zipPath -UseBasicParsing
                    $fi = Get-Item $zipPath
                    if ($fi.Length -gt $MaxZipBytes) { throw "artifact exceeds size cap ($($fi.Length) bytes)" }
                    # Guard against an HTML sign-in page masquerading as a zip. Read only the
                    # first two bytes (PK header) from a stream to avoid allocating the whole file.
                    $stream = [System.IO.File]::OpenRead($zipPath)
                    try {
                        $head = [byte[]]::new(2)
                        $bytesRead = $stream.Read($head, 0, 2)
                    }
                    finally { $stream.Dispose() }
                    if (-not ($bytesRead -ge 2 -and $head[0] -eq 0x50 -and $head[1] -eq 0x4B)) { throw "not a zip (PK header missing)" }
                    Expand-Archive -Path $zipPath -DestinationPath $extractDir -Force
                }
                catch {
                    Write-Log "  [$($artifact.name): download/extract failed: $($_.Exception.Message)]" "DarkYellow"
                    if (Test-Path $zipPath) { Remove-Item $zipPath -Force -ErrorAction SilentlyContinue }
                    continue
                }
                if (Test-Path $zipPath) { Remove-Item $zipPath -Force -ErrorAction SilentlyContinue }

                $trxFiles = @(Get-ChildItem -Path $extractDir -Recurse -Filter *.trx -ErrorAction SilentlyContinue | Select-Object -First $MaxTrxPerArtifact)
                $legFailures = 0
                foreach ($trx in $trxFiles) {
                    if ($trx.Length -gt $MaxTrxBytes) { continue }
                    # Assembly + TFM + arch are encoded in the trx file name, e.g.
                    # Microsoft.Build.Engine.UnitTests_net472_x86.trx
                    $assembly = $trx.BaseName; $tfm = ""; $arch = ""
                    if ($trx.BaseName -match '^(?<asm>.+?)_(?<tfm>net[\w\.]+)_(?<arch>x\d+)$') {
                        $assembly = $Matches.asm; $tfm = $Matches.tfm; $arch = $Matches.arch
                    }
                    try { $failures = Read-TrxFailures -Path $trx.FullName } catch { continue }
                    foreach ($fail in $failures) {
                        $rawName = $fail.FullName
                        $normalized = ($rawName -replace '\(.*\)\s*$', '').Trim()
                        if (-not $testFailures.ContainsKey($normalized)) { $testFailures[$normalized] = @() }
                        $testFailures[$normalized] += [PSCustomObject]@{
                            SourceKey  = $source.Key
                            SourceType = $source.Type
                            Pr         = $source.Pr
                            BuildId    = $buildId
                            BuildUrl   = $buildUrl
                            Leg        = $leg
                            Assembly   = $assembly
                            Tfm        = $tfm
                            Arch       = $arch
                            RawName    = $rawName
                            Message    = $fail.Message
                            ErrorHash  = (Get-ShortHash $fail.Message)
                            Date       = $buildDate
                        }
                        $legFailures++
                    }
                }
                Write-Log "  [${leg}: $legFailures failed result(s) across $($trxFiles.Count) trx]" $(if ($legFailures) { "Yellow" } else { "DarkGreen" })
                Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue
            }
            $scannedBuilds++
        }
    }
}
finally {
    Remove-Item $tmpRoot -Recurse -Force -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
# Step 3: Aggregate and filter by distinct evidence sources.
# ---------------------------------------------------------------------------
Write-Log "`n=== Step 3: Analyzing results ===" "Cyan"
Write-Log "Scanned $scannedBuilds build(s), downloaded $downloads artifact(s), scanComplete=$scanComplete" "White"
Write-Log "Found $($testFailures.Count) distinct test(s) that failed at least once" "White"

$flakyTests = @($testFailures.GetEnumerator() | ForEach-Object {
    $failures = $_.Value
    $distinctSources = @($failures | Select-Object -ExpandProperty SourceKey -Unique)
    $distinctPRs = @($failures | Where-Object { $_.SourceType -eq 'pr' } | Select-Object -ExpandProperty Pr -Unique)
    $rollingBuildIds = @($failures | Where-Object { $_.SourceType -eq 'rolling' } | Select-Object -ExpandProperty BuildId -Unique)
    $dates = @($failures | Where-Object { $_.Date } | Select-Object -ExpandProperty Date -Unique | Sort-Object)
    [PSCustomObject]@{
        TestName        = $_.Key
        DistinctSources = $distinctSources.Count
        DistinctPRs     = $distinctPRs.Count
        PRNumbers       = @($distinctPRs | Sort-Object)
        RollingBuildIds = @($rollingBuildIds | Sort-Object)
        TotalFailures   = $failures.Count
        Legs            = @($failures | Select-Object -ExpandProperty Leg -Unique | Sort-Object)
        Tfms            = @($failures | Where-Object { $_.Tfm } | Select-Object -ExpandProperty Tfm -Unique | Sort-Object)
        Assemblies      = @($failures | Select-Object -ExpandProperty Assembly -Unique | Sort-Object)
        RawVariants     = @($failures | Select-Object -ExpandProperty RawName -Unique)
        ErrorHashes     = @($failures | Where-Object { $_.ErrorHash } | Select-Object -ExpandProperty ErrorHash -Unique)
        FirstSeen       = if ($dates.Count) { $dates[0] } else { "" }
        LastSeen        = if ($dates.Count) { $dates[-1] } else { "" }
        SampleBuildId   = ($failures | Select-Object -First 1).BuildId
        SampleBuildUrl  = ($failures | Select-Object -First 1).BuildUrl
        SampleError     = ($failures | Select-Object -First 1).Message
    }
} | Where-Object { $_.DistinctSources -ge $MinSources } | Sort-Object -Property DistinctSources, TotalFailures -Descending)

# ---------------------------------------------------------------------------
# Step 4: Cross-reference existing flaky-test issues (avoid duplicate filing).
# ---------------------------------------------------------------------------
$existingIssues = @{}
if ($flakyTests.Count -gt 0 -and $ghAvailable) {
    Write-Log "`n=== Step 4: Cross-referencing existing flaky-test issues ===" "Cyan"
    $savedPager = $env:GH_PAGER; $env:GH_PAGER = ""
    foreach ($test in $flakyTests) {
        # Prefer the stable hidden marker, then fall back to the short test name.
        $found = @()
        foreach ($q in @("flaky-test-id: $($test.TestName)", (($test.TestName -split '\.')[-1]))) {
            if ($q.Length -lt 5) { continue }
            try {
                $json = gh issue list --repo $Repo --state all --search "$q in:body,title" --limit 5 --json number,title,state 2>$null
                if ($json) { $found += ($json | ConvertFrom-Json) }
            }
            catch { }
        }
        $found = @($found | Sort-Object -Property number -Unique)
        if ($found.Count -gt 0) { $existingIssues[$test.TestName] = $found }
    }
    $env:GH_PAGER = $savedPager
}

# ---------------------------------------------------------------------------
# Step 5: Human report (stderr) + structured object (stdout / -JsonOut).
# ---------------------------------------------------------------------------
Write-Log "`n============================================" "Magenta"
Write-Log "  FLAKY TEST REPORT" "Magenta"
Write-Log "  Window: last $DaysBack days | Branch: $TargetBranch | Threshold: $MinSources+ sources | scanComplete=$scanComplete" "Magenta"
Write-Log "============================================" "Magenta"

if ($flakyTests.Count -eq 0) {
    Write-Log "`nNo flaky tests detected (threshold: $MinSources distinct sources)." "Green"
}
else {
    Write-Log "`nFound $($flakyTests.Count) flaky test(s):`n" "Red"
    $rank = 1
    foreach ($test in $flakyTests) {
        Write-Log "$rank. $($test.TestName)" "Yellow"
        $srcDesc = "$($test.DistinctSources) sources ($($test.DistinctPRs) PR, $($test.RollingBuildIds.Count) rolling)"
        Write-Log "   $srcDesc | $($test.TotalFailures) failures | $($test.FirstSeen)..$($test.LastSeen)" "White"
        if ($test.PRNumbers.Count) { Write-Log "   PRs: #$($test.PRNumbers -join ', #')" "DarkGray" }
        if ($test.RollingBuildIds.Count) { Write-Log "   Rolling builds: $($test.RollingBuildIds -join ', ')" "DarkGray" }
        Write-Log "   Legs: $($test.Legs -join '; ') | TFMs: $($test.Tfms -join ', ')" "DarkGray"
        if ($existingIssues.ContainsKey($test.TestName)) {
            foreach ($iss in $existingIssues[$test.TestName]) { Write-Log "   >> Existing issue #$($iss.number) [$($iss.state)]: $($iss.title)" "Cyan" }
        }
        else { Write-Log "   >> No existing flaky-test issue found" "DarkRed" }
        Write-Log ""
        $rank++
    }
}
if (-not $scanComplete) {
    Write-Log "WARNING: scan is INCOMPLETE (truncated by MaxBuilds or MaxArtifactDownloads)." "Red"
    Write-Log "Do not file issues or dispatch fixers from this run; widen limits and re-run." "Red"
}

$report = [PSCustomObject]@{
    scanComplete        = $scanComplete
    generatedAt         = (Get-Date).ToUniversalTime().ToString("o")
    definitionId        = $DefinitionId
    targetBranch        = $TargetBranch
    daysBack            = $DaysBack
    minSources          = $MinSources
    buildsScanned       = $scannedBuilds
    prSources           = $keptPRs.Count
    rollingSources      = $rollingBuilds.Count
    artifactsDownloaded = $downloads
    flakyTests          = @($flakyTests | ForEach-Object {
        $related = if ($existingIssues.ContainsKey($_.TestName)) {
            @($existingIssues[$_.TestName] | ForEach-Object { @{ number = $_.number; title = $_.title; state = $_.state } })
        } else { @() }
        [PSCustomObject]@{
            testName        = $_.TestName
            distinctSources = $_.DistinctSources
            distinctPRs     = $_.DistinctPRs
            prNumbers       = $_.PRNumbers
            rollingBuildIds = $_.RollingBuildIds
            totalFailures   = $_.TotalFailures
            legs            = $_.Legs
            tfms            = $_.Tfms
            assemblies      = $_.Assemblies
            rawVariants     = $_.RawVariants
            errorHashes     = $_.ErrorHashes
            firstSeen       = $_.FirstSeen
            lastSeen        = $_.LastSeen
            sampleBuildId   = $_.SampleBuildId
            sampleBuildUrl  = $_.SampleBuildUrl
            sampleError     = $(if ($_.SampleError -and $_.SampleError.Length -gt 500) { $_.SampleError.Substring(0, 500) + "..." } else { $_.SampleError })
            relatedIssues   = $related
        }
    })
}

$json = $report | ConvertTo-Json -Depth 8
if ($JsonOut) {
    $json | Set-Content -Path $JsonOut
    Write-Log "`nStructured report written to: $JsonOut" "DarkGray"
}
$json
