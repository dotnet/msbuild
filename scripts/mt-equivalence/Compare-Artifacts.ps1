<#
.SYNOPSIS
  Byte-level comparison of two MSBuild artifact trees produced by otherwise-identical builds.

.DESCRIPTION
  Walks both trees, hashes every file, and classifies each relative path using an ordered rule set
  (ArtifactCompareRules.json). Rules assign one of four dispositions:

    Compare        - must be byte-identical; any difference fails the comparison.
    ComparePayload - zip container (.nupkg/.vsix/...): the *entries* must be byte-identical, but the
                     zip's own per-entry timestamps may differ (NuGet stamps entries with the source
                     file's last-write time, which is wall-clock).
    Informational  - differences are reported but do not fail. Used only for outputs that are
                     non-deterministic by construction, each with a written reason.
    Ignore         - excluded from the comparison entirely.

  Two automatic reclassifications keep the rule file small and principled:

    * A PE image whose only differing bytes are the 16-byte MVID plus the PE COFF TimeDateStamp and
      checksum was compiled with /deterministic-. Under /deterministic+ the MVID is a hash of the
      emitted content, so an MVID-only delta can never be caused by the build engine: it is a random
      GUID. Such files are downgraded to Informational with that reason recorded.
    * A zip whose entries all hash equal but whose entry timestamps differ is payload-identical.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $BaselineDir,
    [Parameter(Mandatory = $true)][string] $CandidateDir,
    [Parameter(Mandatory = $true)][string] $OutputDir,
    [string] $RulesFile,
    [string] $Label = 'compare',
    [int]    $MaxReportedDiffs = 250,
    [int]    $ThrottleLimit = 0
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

if (-not $RulesFile) { $RulesFile = Join-Path $PSScriptRoot 'ArtifactCompareRules.json' }
foreach ($p in @($BaselineDir, $CandidateDir, $RulesFile)) {
    if (-not (Test-Path -LiteralPath $p)) { throw "Not found: $p" }
}

$BaselineDir = (Resolve-Path $BaselineDir).Path
$CandidateDir = (Resolve-Path $CandidateDir).Path
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Add-Type -TypeDefinition (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'MtCompareNative.cs')) -ErrorAction Stop
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue

# ---------------------------------------------------------------------------------------------
# Rules
# ---------------------------------------------------------------------------------------------

$rules = @((Get-Content -Raw -LiteralPath $RulesFile | ConvertFrom-Json).rules)

function Get-Disposition {
    param([string] $RelativePath)

    foreach ($rule in $rules) {
        if ($RelativePath -like $rule.pattern) {
            $normalizations = @()
            if ($rule.PSObject.Properties.Name -contains 'normalizeEntries') { $normalizations = @($rule.normalizeEntries) }
            return [pscustomobject]@{
                Disposition      = $rule.disposition
                Reason           = $rule.reason
                Pattern          = $rule.pattern
                NormalizeEntries = $normalizations
            }
        }
    }
    return [pscustomobject]@{ Disposition = 'Compare'; Reason = ''; Pattern = '(default)'; NormalizeEntries = @() }
}

# ---------------------------------------------------------------------------------------------
# Index both trees
# ---------------------------------------------------------------------------------------------

function Get-FileIndex {
    param([string] $Root)

    $index = @{}
    foreach ($e in [MtCompareNative]::HashTree($Root, $ThrottleLimit)) { $index[$e.RelativePath] = $e }
    return $index
}

Write-Host "[$Label] indexing $BaselineDir and $CandidateDir ..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$baseIndex = Get-FileIndex -Root $BaselineDir
$candIndex = Get-FileIndex -Root $CandidateDir
$sw.Stop()
Write-Host ("[{0}] indexed {1} baseline / {2} candidate files in {3:n1}s" -f $Label, $baseIndex.Count, $candIndex.Count, $sw.Elapsed.TotalSeconds)

# ---------------------------------------------------------------------------------------------
# Triage helpers
# ---------------------------------------------------------------------------------------------

$script:PeExtensions = @('.dll', '.exe', '.winmd', '.netmodule')
$script:ZipExtensions = @('.nupkg', '.snupkg', '.vsix', '.zip', '.jar')
$script:TextExtensions = @('.txt', '.json', '.xml', '.props', '.targets', '.config', '.md', '.cs', '.rsp',
    '.vsman', '.csv', '.nuspec', '.pkgdef', '.editorconfig', '.log', '.swr', '.man', '.filelist')

function Get-PeLayout {
    param([string] $Path)

    try {
        $fs = [System.IO.File]::OpenRead($Path)
        try {
            $header = New-Object byte[] 4096
            $read = $fs.Read($header, 0, $header.Length)
            if ($read -lt 0x40 -or $header[0] -ne 0x4D -or $header[1] -ne 0x5A) { return $null }

            $peOffset = [System.BitConverter]::ToInt32($header, 0x3C)
            if ($peOffset -le 0 -or ($peOffset + 24) -ge $read) { return $null }
            if ($header[$peOffset] -ne 0x50 -or $header[$peOffset + 1] -ne 0x45) { return $null }

            $coff = $peOffset + 4
            $numberOfSections = [System.BitConverter]::ToUInt16($header, $coff + 2)
            $sizeOfOptionalHeader = [System.BitConverter]::ToUInt16($header, $coff + 16)
            $optionalHeader = $coff + 20
            # CheckSum sits at offset 64 of the optional header for both PE32 and PE32+.
            $checkSumOffset = $optionalHeader + 64
            $sectionTable = $optionalHeader + $sizeOfOptionalHeader

            $sections = New-Object System.Collections.Generic.List[object]
            for ($i = 0; $i -lt $numberOfSections; $i++) {
                $entry = $sectionTable + ($i * 40)
                if (($entry + 40) -gt $read) { break }
                $sections.Add([pscustomobject]@{
                        Name    = [System.Text.Encoding]::ASCII.GetString($header, $entry, 8).TrimEnd([char]0)
                        RawPtr  = [int64][System.BitConverter]::ToUInt32($header, $entry + 20)
                        RawSize = [int64][System.BitConverter]::ToUInt32($header, $entry + 16)
                    })
            }

            return [pscustomobject]@{
                TimeDateStampOffset = [int64]($coff + 4)
                CheckSumOffset      = [int64]$checkSumOffset
                Sections            = $sections
                HeaderEnd           = [int64]($sectionTable + ($numberOfSections * 40))
            }
        }
        finally { $fs.Dispose() }
    }
    catch { return $null }
}

function Get-RegionName {
    param($Layout, [int64] $Offset)

    if (-not $Layout) { return 'unknown' }
    if ($Offset -ge $Layout.TimeDateStampOffset -and $Offset -lt ($Layout.TimeDateStampOffset + 4)) { return 'coff:TimeDateStamp' }
    if ($Offset -ge $Layout.CheckSumOffset -and $Offset -lt ($Layout.CheckSumOffset + 4)) { return 'optional:CheckSum' }
    foreach ($s in $Layout.Sections) {
        if ($s.RawSize -gt 0 -and $Offset -ge $s.RawPtr -and $Offset -lt ($s.RawPtr + $s.RawSize)) { return $s.Name }
    }
    if ($Offset -lt $Layout.HeaderEnd) { return 'headers' }
    return 'unmapped'
}

function Compare-PeFile {
    param([string] $LeftPath, [string] $RightPath, [long] $LeftLength, [long] $RightLength)

    $layout = Get-PeLayout -Path $LeftPath
    $runs = [MtCompareNative]::DiffRuns($LeftPath, $RightPath, 256)

    $regionRuns = @()
    $totalBytes = 0
    foreach ($run in $runs) {
        $regionRuns += [pscustomobject]@{
            Region = (Get-RegionName -Layout $layout -Offset $run.Start)
            Start  = $run.Start
            Length = $run.Length
        }
        $totalBytes += $run.Length
    }

    # /deterministic- signature: only the COFF TimeDateStamp (which Roslyn derives from the MVID),
    # the optional-header CheckSum, and exactly one 16-byte run (the MVID in the #GUID heap) differ.
    $payloadRuns = @($regionRuns | Where-Object { $_.Region -notin @('coff:TimeDateStamp', 'optional:CheckSum') })
    # Coincidentally-equal bytes can split the 16-byte GUID into several runs, so require the payload
    # differences to be confined to a single 16-byte window rather than to a single run.
    $mvidOnly = $false
    if ($runs.Count -gt 0 -and $payloadRuns.Count -gt 0 -and $LeftLength -eq $RightLength) {
        $windowStart = ($payloadRuns | Measure-Object -Property Start -Minimum).Minimum
        $windowEnd = ($payloadRuns | ForEach-Object { $_.Start + $_.Length } | Measure-Object -Maximum).Maximum
        $stampDiffered = @($regionRuns | Where-Object { $_.Region -eq 'coff:TimeDateStamp' }).Count -gt 0
        $mvidOnly = $stampDiffered -and (($windowEnd - $windowStart) -le 16)
    }

    [pscustomobject]@{
        Kind             = 'pe'
        DifferingRuns    = $runs.Count
        DifferingBytes   = $totalBytes
        Regions          = (($regionRuns | Select-Object -First 12 | ForEach-Object { '{0}@0x{1:X}+{2}' -f $_.Region, $_.Start, $_.Length }) -join ', ')
        MvidAndStampOnly = $mvidOnly
    }
}

function Compare-ZipFile {
    param([string] $LeftPath, [string] $RightPath, $NormalizeEntries = @())

    # Entry normalizations let a rule tolerate a documented, provably-irrelevant per-build value inside
    # a specific entry (for example the random OPC relationship id in a .vsix) while still comparing the
    # rest of that entry, and every other entry, byte for byte.
    $normalizers = @()
    foreach ($n in $NormalizeEntries) {
        $normalizers += [pscustomobject]@{
            EntryPattern = $n.entryPattern
            Regex        = [regex]::new($n.pattern)
            Replacement  = $n.replacement
        }
    }

    function Get-ZipEntries([string] $Path) {
        $result = @{}
        $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
        try {
            foreach ($entry in $archive.Entries) {
                $applicable = @($normalizers | Where-Object { $entry.FullName -like $_.EntryPattern })
                $stream = $entry.Open()
                try {
                    if ($applicable.Count -gt 0) {
                        $reader = New-Object System.IO.StreamReader($stream)
                        try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
                        foreach ($n in $applicable) { $content = $n.Regex.Replace($content, $n.Replacement) }
                        $bytes = [System.Text.Encoding]::UTF8.GetBytes($content)
                        $hash = [System.BitConverter]::ToString(([System.Security.Cryptography.SHA256]::Create()).ComputeHash($bytes)).Replace('-', '')
                    }
                    else {
                        $hash = [System.BitConverter]::ToString(([System.Security.Cryptography.SHA256]::Create()).ComputeHash($stream)).Replace('-', '')
                    }
                }
                finally { $stream.Dispose() }
                $result[$entry.FullName] = [pscustomobject]@{ Hash = $hash; Time = $entry.LastWriteTime.UtcDateTime.ToString('o') }
            }
        }
        finally { $archive.Dispose() }
        return $result
    }

    try {
        $left = Get-ZipEntries $LeftPath
        $right = Get-ZipEntries $RightPath
    }
    catch {
        return [pscustomobject]@{
            Kind = 'zip'; Error = $_.Exception.Message; PayloadIdentical = $false
            EntriesWithContentDiff = @(); EntriesOnlyInBaseline = @(); EntriesOnlyInCandidate = @(); EntriesWithTimestampDiff = @()
        }
    }

    $onlyLeft = @($left.Keys | Where-Object { -not $right.ContainsKey($_) } | Sort-Object)
    $onlyRight = @($right.Keys | Where-Object { -not $left.ContainsKey($_) } | Sort-Object)
    $contentDiff = New-Object System.Collections.Generic.List[string]
    $timeDiff = New-Object System.Collections.Generic.List[string]

    foreach ($key in ($left.Keys | Sort-Object)) {
        if (-not $right.ContainsKey($key)) { continue }
        if ($left[$key].Hash -ne $right[$key].Hash) { $contentDiff.Add($key) }
        elseif ($left[$key].Time -ne $right[$key].Time) { $timeDiff.Add($key) }
    }

    [pscustomobject]@{
        Kind                     = 'zip'
        EntryCount               = $left.Count
        EntriesOnlyInBaseline    = $onlyLeft
        EntriesOnlyInCandidate   = $onlyRight
        EntriesWithContentDiff   = @($contentDiff)
        EntriesWithTimestampDiff = @($timeDiff)
        PayloadIdentical         = ($onlyLeft.Count -eq 0 -and $onlyRight.Count -eq 0 -and $contentDiff.Count -eq 0)
    }
}

function Compare-TextFile {
    param([string] $LeftPath, [string] $RightPath)

    try {
        $left = [System.IO.File]::ReadAllLines($LeftPath)
        $right = [System.IO.File]::ReadAllLines($RightPath)
    }
    catch { return [pscustomobject]@{ Kind = 'binary' } }

    $max = [Math]::Max($left.Length, $right.Length)
    $diffs = New-Object System.Collections.Generic.List[object]
    for ($i = 0; $i -lt $max -and $diffs.Count -lt 5; $i++) {
        $l = $(if ($i -lt $left.Length) { $left[$i] } else { '<missing>' })
        $r = $(if ($i -lt $right.Length) { $right[$i] } else { '<missing>' })
        if ($l -ne $r) { $diffs.Add([pscustomobject]@{ Line = $i + 1; Baseline = $l; Candidate = $r }) }
    }
    [pscustomobject]@{ Kind = 'text'; FirstDifferences = $diffs.ToArray() }
}

function Get-DiffDetail {
    param([string] $Rel, [string] $LeftPath, [string] $RightPath, [long] $LeftLength, [long] $RightLength, $NormalizeEntries = @())

    $ext = [System.IO.Path]::GetExtension($Rel).ToLowerInvariant()
    try {
        if ($ext -in $script:ZipExtensions) { return Compare-ZipFile -LeftPath $LeftPath -RightPath $RightPath -NormalizeEntries $NormalizeEntries }
        if ($ext -in $script:PeExtensions) { return Compare-PeFile -LeftPath $LeftPath -RightPath $RightPath -LeftLength $LeftLength -RightLength $RightLength }
        if ($ext -in $script:TextExtensions) { return Compare-TextFile -LeftPath $LeftPath -RightPath $RightPath }
    }
    catch { return [pscustomobject]@{ Kind = 'error'; Error = $_.Exception.Message } }
    return [pscustomobject]@{ Kind = 'binary' }
}

# ---------------------------------------------------------------------------------------------
# Comparison
# ---------------------------------------------------------------------------------------------

$allPaths = [System.Collections.Generic.HashSet[string]]::new([string[]]@($baseIndex.Keys))
$allPaths.UnionWith([string[]]@($candIndex.Keys))

$results = New-Object System.Collections.Generic.List[object]
$comparedCount = 0

# Ignored paths are the one class of exclusion that leaves no trace in the diff lists, so they are
# tallied per rule and reported. Without this, an Ignore pattern that quietly grew to cover a real
# build output would be invisible in every run.
$ignoredByRule = [ordered]@{}
foreach ($rule in $rules) {
    if ($rule.disposition -eq 'Ignore') {
        $ignoredByRule[$rule.pattern] = [pscustomobject]@{
            Pattern         = $rule.pattern
            Reason          = $rule.reason
            Count           = 0
            OnlyInBaseline  = 0
            OnlyInCandidate = 0
            Differing       = 0
            Samples         = (New-Object System.Collections.Generic.List[string])
        }
    }
}

foreach ($rel in ($allPaths | Sort-Object)) {
    $disp = Get-Disposition -RelativePath $rel
    if ($disp.Disposition -eq 'Ignore') {
        $bucket = $ignoredByRule[$disp.Pattern]
        if ($bucket) {
            $bucket.Count++
            $inB = $baseIndex.ContainsKey($rel)
            $inC = $candIndex.ContainsKey($rel)
            if ($inB -and -not $inC) { $bucket.OnlyInBaseline++ }
            elseif ($inC -and -not $inB) { $bucket.OnlyInCandidate++ }
            elseif ($baseIndex[$rel].Hash -ne $candIndex[$rel].Hash) { $bucket.Differing++ }
            if ($bucket.Samples.Count -lt 25) { $bucket.Samples.Add($rel) }
        }
        continue
    }
    $comparedCount++

    $inBase = $baseIndex.ContainsKey($rel)
    $inCand = $candIndex.ContainsKey($rel)

    if ($inBase -and $inCand) {
        if ($baseIndex[$rel].Hash -eq $candIndex[$rel].Hash) { continue }
        $status = 'ContentDiffers'
    }
    elseif ($inBase) { $status = 'OnlyInBaseline' }
    else { $status = 'OnlyInCandidate' }

    $results.Add([pscustomobject]@{
            Rel              = $rel
            Status           = $status
            Disposition      = $disp.Disposition
            Reason           = $disp.Reason
            Pattern          = $disp.Pattern
            NormalizeEntries = $disp.NormalizeEntries
            BaseLength       = $(if ($inBase) { $baseIndex[$rel].Length } else { $null })
            CandLength       = $(if ($inCand) { $candIndex[$rel].Length } else { $null })
            Detail           = $null
        })
}

Write-Host ("[{0}] {1} paths excluded by Ignore rules: {2}" -f $Label, (($ignoredByRule.Values | Measure-Object -Property Count -Sum).Sum), `
    (($ignoredByRule.Values | ForEach-Object { "$($_.Pattern)=$($_.Count)" }) -join ', '))
Write-Host "[$Label] triaging $($results.Count) differing paths ..."
$sw = [System.Diagnostics.Stopwatch]::StartNew()

foreach ($r in $results) {
    if ($r.Status -ne 'ContentDiffers') { continue }
    $r.Detail = Get-DiffDetail -Rel $r.Rel -LeftPath $baseIndex[$r.Rel].FullPath -RightPath $candIndex[$r.Rel].FullPath -LeftLength $r.BaseLength -RightLength $r.CandLength -NormalizeEntries $r.NormalizeEntries

    if ($r.Disposition -eq 'ComparePayload') {
        if ($r.Detail.Kind -eq 'zip' -and $r.Detail.PayloadIdentical) {
            $r.Disposition = 'Informational'
            $r.Reason = "Zip payload is byte-identical$(if ($r.NormalizeEntries.Count -gt 0) { ' after the documented per-entry normalization' }); only the container's per-entry metadata differs. Rule reason: $($r.Reason)"
        }
        else {
            $r.Disposition = 'Compare'
        }
    }
    elseif ($r.Disposition -eq 'Compare' -and $r.Detail.Kind -eq 'pe' -and $r.Detail.MvidAndStampOnly) {
        $r.Disposition = 'Informational'
        $r.Reason = 'Only the 16-byte assembly MVID and the PE TimeDateStamp/CheckSum differ: this assembly is compiled with /deterministic- so its MVID is a random GUID. Emitted IL, metadata and resources are byte-identical.'
    }
}

$sw.Stop()
Write-Host ("[{0}] triage completed in {1:n1}s" -f $Label, $sw.Elapsed.TotalSeconds)

$failures = @($results | Where-Object { $_.Disposition -ne 'Informational' })
$informational = @($results | Where-Object { $_.Disposition -eq 'Informational' })

$ignoredRules = @($ignoredByRule.Values)
$ignoredCount = 0
foreach ($b in $ignoredRules) { $ignoredCount += $b.Count }

$summary = [pscustomobject]@{
    Label                  = $Label
    BaselineDir            = $BaselineDir
    CandidateDir           = $CandidateDir
    BaselineFileCount      = $baseIndex.Count
    CandidateFileCount     = $candIndex.Count
    ComparedPathCount      = $comparedCount
    IgnoredPathCount       = $ignoredCount
    IdenticalPathCount     = $comparedCount - $results.Count
    FailingDiffCount       = $failures.Count
    InformationalDiffCount = $informational.Count
    Passed                 = ($failures.Count -eq 0)
    GeneratedUtc           = (Get-Date).ToUniversalTime().ToString('o')
}

$report = [pscustomobject]@{ Summary = $summary; Failures = $failures; Informational = $informational; Ignored = $ignoredRules }
$jsonPath = Join-Path $OutputDir "artifact-compare.$Label.json"
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding utf8

# ---------------------------------------------------------------------------------------------
# Markdown report
# ---------------------------------------------------------------------------------------------

$md = New-Object System.Collections.Generic.List[string]
$md.Add("## Artifact comparison: ``$Label``")
$md.Add('')
$md.Add('| | |')
$md.Add('|---|---|')
$md.Add("| Baseline | ``$BaselineDir`` ($($baseIndex.Count) files) |")
$md.Add("| Candidate | ``$CandidateDir`` ($($candIndex.Count) files) |")
$md.Add("| Paths compared | $comparedCount |")
$md.Add("| Paths excluded by an Ignore rule | $ignoredCount |")
$md.Add("| Byte-identical | $($summary.IdenticalPathCount) |")
$md.Add("| **Unexpected differences** | **$($failures.Count)** |")
$md.Add("| Expected differences (documented) | $($informational.Count) |")
$md.Add("| Result | $(if ($summary.Passed) { 'PASS' } else { 'FAIL' }) |")
$md.Add('')

function Add-DiffSection {
    param($Items, [string] $Title)

    if ($Items.Count -eq 0) { return }
    $md.Add("### $Title ($($Items.Count))")
    $md.Add('')
    foreach ($item in ($Items | Select-Object -First $MaxReportedDiffs)) {
        $md.Add("- ``$($item.Rel)`` - **$($item.Status)**")
        if ($item.Reason) { $md.Add("  - reason: $($item.Reason)") }
        if ($item.Detail) {
            switch ($item.Detail.Kind) {
                'pe' { $md.Add("  - PE: $($item.Detail.DifferingBytes) bytes in $($item.Detail.DifferingRuns) run(s): $($item.Detail.Regions)") }
                'zip' {
                    if ($item.Detail.PSObject.Properties.Name -contains 'Error') { $md.Add("  - zip: $($item.Detail.Error)") }
                    else {
                        $md.Add("  - zip: payloadIdentical=$($item.Detail.PayloadIdentical); contentDiff=$($item.Detail.EntriesWithContentDiff.Count); timestampOnly=$($item.Detail.EntriesWithTimestampDiff.Count); onlyBaseline=$($item.Detail.EntriesOnlyInBaseline.Count); onlyCandidate=$($item.Detail.EntriesOnlyInCandidate.Count)")
                        foreach ($e in ($item.Detail.EntriesWithContentDiff | Select-Object -First 10)) { $md.Add("    - entry content differs: ``$e``") }
                        foreach ($e in ($item.Detail.EntriesOnlyInBaseline | Select-Object -First 10)) { $md.Add("    - entry only in baseline: ``$e``") }
                        foreach ($e in ($item.Detail.EntriesOnlyInCandidate | Select-Object -First 10)) { $md.Add("    - entry only in candidate: ``$e``") }
                    }
                }
                'text' { foreach ($d in $item.Detail.FirstDifferences) { $md.Add("    - line $($d.Line): baseline ``$($d.Baseline)`` / candidate ``$($d.Candidate)``") } }
                default { $md.Add("  - $($item.Detail.Kind): baseline $($item.BaseLength) bytes, candidate $($item.CandLength) bytes") }
            }
        }
    }
    if ($Items.Count -gt $MaxReportedDiffs) { $md.Add("- _(... $($Items.Count - $MaxReportedDiffs) more; see the JSON report)_") }
    $md.Add('')
}

Add-DiffSection -Items $failures -Title 'Unexpected differences'
Add-DiffSection -Items $informational -Title 'Expected differences'

# Everything the Ignore rules removed from the comparison, so the exclusions can be reviewed without
# having to re-run the build. 'differing' is how many of them would have been reported had the rule
# not existed - a rule with a large 'differing' count is doing real work and deserves scrutiny; a
# rule matching nothing is dead and should be deleted.
$md.Add('### Paths excluded from the comparison')
$md.Add('')
$md.Add('| Ignore rule | matched | differing | only in baseline | only in candidate |')
$md.Add('|---|---|---|---|---|')
foreach ($b in $ignoredRules) {
    $md.Add("| ``$($b.Pattern)`` | $($b.Count) | $($b.Differing) | $($b.OnlyInBaseline) | $($b.OnlyInCandidate) |")
}
$md.Add('')
foreach ($b in $ignoredRules) {
    if ($b.Count -eq 0) {
        $md.Add("- ``$($b.Pattern)`` matched nothing in this build.")
        continue
    }
    $md.Add("- ``$($b.Pattern)`` - $($b.Reason)")
    foreach ($s in $b.Samples) { $md.Add("  - ``$s``") }
    if ($b.Count -gt $b.Samples.Count) { $md.Add("  - _(... $($b.Count - $b.Samples.Count) more; see the JSON report)_") }
}
$md.Add('')

$mdPath = Join-Path $OutputDir "artifact-compare.$Label.md"
($md -join [Environment]::NewLine) | Set-Content -LiteralPath $mdPath -Encoding utf8

Write-Host ''
Write-Host "[$Label] $comparedCount paths compared: $($summary.IdenticalPathCount) identical, $($failures.Count) unexpected, $($informational.Count) expected"
Write-Host "[$Label] reports: $jsonPath"
Write-Host "[$Label]          $mdPath"

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host "[$Label] FAIL - unexpected differences (first 25):"
    foreach ($f in ($failures | Select-Object -First 25)) { Write-Host "  $($f.Status.PadRight(16)) $($f.Rel)" }
    exit 1
}

Write-Host "[$Label] PASS"
exit 0
