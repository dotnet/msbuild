# Copyright (c) Microsoft. All rights reserved.

[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ClusterUri,

    [Parameter(Mandatory)]
    [string]$Database,

    [Parameter(Mandatory)]
    [string]$QueryPath,

    [Parameter(Mandatory)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$accessToken = $env:KUSTO_ACCESS_TOKEN
if ([string]::IsNullOrWhiteSpace($accessToken))
{
    throw 'KUSTO_ACCESS_TOKEN is required.'
}

if (-not (Test-Path -LiteralPath $QueryPath))
{
    throw "Kusto query file not found: $QueryPath"
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$query = Get-Content -LiteralPath $QueryPath -Raw
$payload = @{
    db = $Database
    csl = $query
} | ConvertTo-Json -Compress

$response = Invoke-RestMethod `
    -Method Post `
    -Uri "$($ClusterUri.TrimEnd('/'))/v1/rest/query" `
    -Headers @{ Authorization = "Bearer $accessToken" } `
    -ContentType 'application/json' `
    -Body $payload

$table = $response.Tables | Select-Object -First 1
if ($null -eq $table)
{
    throw 'Kusto returned no primary result table.'
}

$columnNames = @($table.Columns | ForEach-Object { $_.ColumnName })
$candidates = @(
    foreach ($row in $table.Rows)
    {
        $record = [ordered]@{}
        for ($index = 0; $index -lt $columnNames.Count; $index++)
        {
            $record[$columnNames[$index]] = $row[$index]
        }

        [pscustomobject]$record
    }
)

$generatedAtUtc = [DateTimeOffset]::UtcNow
$candidateKeyInputs = @(
    $candidates |
        ForEach-Object { "$($_.Backend)/$($_.Os)/$($_.ScenarioPair)" } |
        Sort-Object -Unique)
$candidateKeyText = $candidateKeyInputs -join "`n"
$candidateKeyBytes = [System.Text.Encoding]::UTF8.GetBytes($candidateKeyText)
$candidateSetKey = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($candidateKeyBytes))
$candidateSetKey = $candidateSetKey.Substring(0, 16).ToLowerInvariant()

$report = [ordered]@{
    generatedAtUtc = $generatedAtUtc.ToString('o')
    candidateSetKey = $candidateSetKey
    candidateKeyInputs = $candidateKeyInputs
    # Keep this metadata and the Markdown wording below synchronized with
    # Get-MtBuildTimeRegressions.kql, which is the executable source of truth.
    detector = [ordered]@{
        lookbackDays = 21
        freshnessDays = 2
        minimumBaselineRuns = 4
        minimumMtRegressionPercent = 5.0
        minimumMtRegressionMs = 250.0
        requiresCurrentMtAboveBaselineP90 = $true
        requiresMtVsNonMtDifferentialRegression = $true
    }
    candidateCount = $candidates.Count
    candidates = $candidates
}

$jsonPath = Join-Path $OutputDirectory 'mt-regressions.json'
$markdownPath = Join-Path $OutputDirectory 'mt-regression-context.md'
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

$markdown = [System.Text.StringBuilder]::new()
[void]$markdown.AppendLine('# PerfStar possible MT build-time regressions')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("Generated: $($generatedAtUtc.ToString('u'))")
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('These are statistical candidates, not confirmed product regressions. The detector compares the latest production MT/non-MT pair with paired production runs from the preceding 21 days. A candidate must:')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine('- regress by at least 5% and 250 ms against the historical MT median;')
[void]$markdown.AppendLine('- exceed the historical MT p90;')
[void]$markdown.AppendLine('- have at least four paired baseline runs; and')
[void]$markdown.AppendLine('- show at least 250 ms of deterioration in the MT-minus-non-MT differential.')
[void]$markdown.AppendLine()
[void]$markdown.AppendLine("Candidate count: **$($candidates.Count)**")
[void]$markdown.AppendLine("Candidate-set key: ``$candidateSetKey``")

if ($candidates.Count -gt 0)
{
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine('| Severity | Backend | OS | Scenario pair | Current MT | Baseline MT | MT regression | Differential regression | Build |')
    [void]$markdown.AppendLine('| --- | --- | --- | --- | ---: | ---: | ---: | ---: | --- |')

    foreach ($candidate in $candidates)
    {
        $buildLink = "[$($candidate.CurrentBuildNumber)]($($candidate.BuildUrl))"
        [void]$markdown.AppendLine(
            "| $($candidate.Severity) | $($candidate.Backend) | $($candidate.Os) | ``$($candidate.ScenarioPair)`` | " +
            "$($candidate.CurrentMtMedianMs) ms | $($candidate.BaselineMtMedianMs) ms | " +
            "+$($candidate.MtRegressionMs) ms ($($candidate.MtRegressionPercent)%) | " +
            "+$($candidate.DifferentialRegressionMs) ms | $buildLink |")
    }

    foreach ($candidate in $candidates)
    {
        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine("## $($candidate.Backend)/$($candidate.Os): ``$($candidate.ScenarioPair)``")
        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine("- MT scenario: ``$($candidate.MtScenario)``")
        [void]$markdown.AppendLine("- Non-MT scenario: ``$($candidate.NonMtScenario)``")
        [void]$markdown.AppendLine("- Current MT/non-MT medians: $($candidate.CurrentMtMedianMs) ms / $($candidate.CurrentNonMtMedianMs) ms")
        [void]$markdown.AppendLine("- Historical MT median/p90: $($candidate.BaselineMtMedianMs) ms / $($candidate.BaselineMtP90Ms) ms")
        [void]$markdown.AppendLine("- Historical non-MT median: $($candidate.BaselineNonMtMedianMs) ms")
        [void]$markdown.AppendLine("- Current/historical MT-minus-non-MT delta: $($candidate.CurrentMtVsNonMtDeltaMs) ms / $($candidate.BaselineDeltaMedianMs) ms")
        [void]$markdown.AppendLine("- Current non-MT change from baseline: $($candidate.NonMtRegressionMs) ms")
        [void]$markdown.AppendLine("- Paired baseline runs: $($candidate.BaselineRunCount)")
        [void]$markdown.AppendLine("- Build: [$($candidate.CurrentBuildNumber)]($($candidate.BuildUrl))")
    }
}

$markdown.ToString() | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM

if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_OUTPUT))
{
    "has_regressions=$($candidates.Count -gt 0 ? 'true' : 'false')" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
    "regression_count=$($candidates.Count)" | Add-Content -LiteralPath $env:GITHUB_OUTPUT
}

Write-Host "Wrote $($candidates.Count) candidate(s) to $OutputDirectory."
