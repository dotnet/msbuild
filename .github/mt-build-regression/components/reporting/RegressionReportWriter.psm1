# Copyright (c) Microsoft. All rights reserved.

function Write-RegressionDetectionReport
{
    param(
        [Parameter(Mandatory)]$Report,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $jsonPath = Join-Path $OutputDirectory 'mt-regressions.json'
    $markdownPath = Join-Path $OutputDirectory 'mt-regression-context.md'
    $Report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

    $candidates = @($Report.candidates)
    $generatedAtUtc = [DateTimeOffset]::Parse([string]$Report.generatedAtUtc)
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
    [void]$markdown.AppendLine("Candidate-set key: ``$($Report.candidateSetKey)``")

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
}

function Get-CompareUrl
{
    [OutputType([string])]
    param($HealthyRun, $CurrentRun)

    if ($null -eq $HealthyRun -or
        [string]::IsNullOrWhiteSpace($HealthyRun.componentSourceVersion) -or
        [string]::IsNullOrWhiteSpace($CurrentRun.componentSourceVersion))
    {
        return ''
    }

    "https://github.com/dotnet/msbuild/compare/$($HealthyRun.componentSourceVersion)...$($CurrentRun.componentSourceVersion)"
}

function Add-MetricTable
{
    param(
        [Parameter(Mandatory)][System.Text.StringBuilder]$Builder,
        $CurrentMt,
        $CurrentNonMt,
        $HealthyMt,
        $HealthyNonMt
    )

    $metricNames = @(
        'build-time',
        'evaluation-time',
        'evaluation-time-globbing',
        'evaluation-time-pass0',
        'evaluation-time-pass1',
        'evaluation-time-pass2',
        'evaluation-time-pass3',
        'evaluation-time-pass3dot1',
        'evaluation-time-pass4',
        'evaluation-time-pass5',
        'exit-code',
        'msbuild-version',
        'dotnet-version'
    )

    [void]$Builder.AppendLine('| Metric | Current MT | Current non-MT | Healthy MT | Healthy non-MT |')
    [void]$Builder.AppendLine('| --- | ---: | ---: | ---: | ---: |')
    foreach ($metricName in $metricNames)
    {
        $currentMtValue = if ($null -ne $CurrentMt -and $CurrentMt.available) { $CurrentMt.metrics.$metricName } else { '' }
        $currentNonMtValue = if ($null -ne $CurrentNonMt -and $CurrentNonMt.available) { $CurrentNonMt.metrics.$metricName } else { '' }
        $healthyMtValue = if ($null -ne $HealthyMt -and $HealthyMt.available) { $HealthyMt.metrics.$metricName } else { '' }
        $healthyNonMtValue = if ($null -ne $HealthyNonMt -and $HealthyNonMt.available) { $HealthyNonMt.metrics.$metricName } else { '' }
        if (@($currentMtValue, $currentNonMtValue, $healthyMtValue, $healthyNonMtValue) | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
        {
            [void]$Builder.AppendLine("| ``$metricName`` | $currentMtValue | $currentNonMtValue | $healthyMtValue | $healthyNonMtValue |")
        }
    }
}

function Write-ActualRunEvidenceReport
{
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Candidates,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $report = [ordered]@{
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        candidateCount = $Candidates.Count
        candidates = $Candidates
    }
    $jsonPath = Join-Path $OutputDirectory 'mt-regression-evidence.json'
    $markdownPath = Join-Path $OutputDirectory 'mt-regression-evidence.md'
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

    $markdown = [System.Text.StringBuilder]::new()
    [void]$markdown.AppendLine('# PerfStar actual-run evidence')
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine('Raw Azure DevOps artifacts were downloaded into an ephemeral directory, transformed, sanitized, bounded, and deleted. Only this derived report is uploaded to GitHub.')

    foreach ($candidate in $Candidates)
    {
        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine("## $($candidate.Backend)/$($candidate.Os): ``$($candidate.ScenarioPair)``")
        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine("- Current PerfStar run: [$($candidate.currentRun.perfStarBuildNumber)]($($candidate.currentRun.perfStarBuildUrl))")
        [void]$markdown.AppendLine("- Current MSBuild build: [$($candidate.currentRun.componentBuildNumber)]($($candidate.currentRun.componentBuildUrl))")
        [void]$markdown.AppendLine("- Current MSBuild source: ``$($candidate.currentRun.componentSourceVersion)``")

        if ($null -ne $candidate.healthyRun)
        {
            [void]$markdown.AppendLine("- Last healthy PerfStar run: [$($candidate.healthyRun.perfStarBuildNumber)]($($candidate.healthyRun.perfStarBuildUrl))")
            [void]$markdown.AppendLine("- Last healthy MSBuild build: [$($candidate.healthyRun.componentBuildNumber)]($($candidate.healthyRun.componentBuildUrl))")
            [void]$markdown.AppendLine("- Last healthy MSBuild source: ``$($candidate.healthyRun.componentSourceVersion)``")
            $compareUrl = Get-CompareUrl -HealthyRun $candidate.healthyRun -CurrentRun $candidate.currentRun
            if (-not [string]::IsNullOrWhiteSpace($compareUrl))
            {
                [void]$markdown.AppendLine("- Source comparison: [healthy...current]($compareUrl)")
            }
        }

        [void]$markdown.AppendLine()
        Add-MetricTable `
            -Builder $markdown `
            -CurrentMt $candidate.currentMtEvidence `
            -CurrentNonMt $candidate.currentNonMtEvidence `
            -HealthyMt $candidate.healthyMtEvidence `
            -HealthyNonMt $candidate.healthyNonMtEvidence

        foreach ($entry in @(
            @{ Name = 'Current MT log excerpt'; Evidence = $candidate.currentMtEvidence },
            @{ Name = 'Current non-MT log excerpt'; Evidence = $candidate.currentNonMtEvidence }))
        {
            if ($null -ne $entry.Evidence -and -not [string]::IsNullOrWhiteSpace([string]$entry.Evidence.logExcerpt))
            {
                [void]$markdown.AppendLine()
                [void]$markdown.AppendLine("<details><summary>$($entry.Name)</summary>")
                [void]$markdown.AppendLine()
                [void]$markdown.AppendLine('```text')
                [void]$markdown.AppendLine($entry.Evidence.logExcerpt)
                [void]$markdown.AppendLine('```')
                [void]$markdown.AppendLine('</details>')
            }
        }
    }

    $markdown.ToString() | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM
}

function Add-DeltaTable
{
    param(
        [Parameter(Mandatory)][System.Text.StringBuilder]$Builder,
        [Parameter(Mandatory)][string]$Heading,
        [Parameter(Mandatory)]$Rows,
        [bool]$IncludeMigrationState
    )

    if (@($Rows).Count -eq 0)
    {
        return
    }

    [void]$Builder.AppendLine()
    [void]$Builder.AppendLine("### $Heading")
    [void]$Builder.AppendLine()
    if ($IncludeMigrationState)
    {
        [void]$Builder.AppendLine('| Item | non-MT ms | MT ms | Delta | Migration state |')
        [void]$Builder.AppendLine('| --- | ---: | ---: | ---: | --- |')
    }
    else
    {
        [void]$Builder.AppendLine('| Item | non-MT ms | MT ms | Delta |')
        [void]$Builder.AppendLine('| --- | ---: | ---: | ---: |')
    }

    foreach ($row in @($Rows))
    {
        $itemName = ([string]$row.item_name).Replace('|', '\|')
        $nonMt = [Math]::Round([double]$row.non_mt_ms, 1)
        $mt = [Math]::Round([double]$row.mt_ms, 1)
        $delta = [Math]::Round([double]$row.delta_ms, 1)
        if ($IncludeMigrationState)
        {
            [void]$Builder.AppendLine("| ``$itemName`` | $nonMt | $mt | $delta | $($row.migration_state) |")
        }
        else
        {
            [void]$Builder.AppendLine("| ``$itemName`` | $nonMt | $mt | $delta |")
        }
    }
}

function Add-DiagnosticSection
{
    param(
        [Parameter(Mandatory)][System.Text.StringBuilder]$Builder,
        [Parameter(Mandatory)][string]$Heading,
        $Diagnostic
    )

    [void]$Builder.AppendLine()
    [void]$Builder.AppendLine("### $Heading")
    [void]$Builder.AppendLine()
    if ($null -eq $Diagnostic)
    {
        [void]$Builder.AppendLine('No exact-source diagnostic run with task-level Kusto data was found.')
        return
    }

    [void]$Builder.AppendLine("- Diagnostic run: [$($Diagnostic.diagnosticBuildNumber)]($($Diagnostic.diagnosticBuildUrl))")
    [void]$Builder.AppendLine("- Result: ``$($Diagnostic.diagnosticResult)`` (partial scenario data is accepted)")
    [void]$Builder.AppendLine("- Relationship: ``$($Diagnostic.relationship)``")
    [void]$Builder.AppendLine("- MSBuild source: ``$($Diagnostic.componentSourceVersion)``")
    [void]$Builder.AppendLine()
    [void]$Builder.AppendLine('Task and target durations are aggregated across instances and may include nested/inclusive work. Positive deltas must be compared with migrated controls before attribution.')

    Add-DeltaTable -Builder $Builder -Heading 'Largest task deltas' -Rows $Diagnostic.topTaskDeltas -IncludeMigrationState $true
    Add-DeltaTable -Builder $Builder -Heading 'Migrated task controls (contention/noise floor)' -Rows $Diagnostic.migratedTaskControls -IncludeMigrationState $true
    Add-DeltaTable -Builder $Builder -Heading 'Largest target deltas' -Rows $Diagnostic.topTargetDeltas -IncludeMigrationState $false
    Add-DeltaTable -Builder $Builder -Heading 'Evaluation-pass deltas' -Rows $Diagnostic.evaluationPassDeltas -IncludeMigrationState $false
}

function Write-DiagnosticEvidenceReport
{
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Candidates,
        [Parameter(Mandatory)][int]$DiagnosticPipelineId,
        [Parameter(Mandatory)][int]$MaximumRunsToInspect,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $report = [ordered]@{
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
        diagnosticPipelineId = $DiagnosticPipelineId
        inspectedRunLimit = $MaximumRunsToInspect
        candidateCount = $Candidates.Count
        candidates = $Candidates
    }
    $jsonPath = Join-Path $OutputDirectory 'mt-regression-diagnostics.json'
    $markdownPath = Join-Path $OutputDirectory 'mt-regression-diagnostics.md'
    $report | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $jsonPath -Encoding utf8NoBOM

    $markdown = [System.Text.StringBuilder]::new()
    [void]$markdown.AppendLine('# PerfStar scheduled-binlog supporting evidence')
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine("Diagnostic pipeline: [definition $DiagnosticPipelineId](https://dev.azure.com/devdiv/DevDiv/_build?definitionId=$DiagnosticPipelineId)")
    [void]$markdown.AppendLine()
    [void]$markdown.AppendLine('Diagnostic runs are matched by exact MSBuild source SHA. A failed overall diagnostic run is usable when the affected scenario/OS has task-level Kusto data. Hosted evidence is direct; for Gold candidates it is corroboration from the Hosted backend.')

    foreach ($candidate in $Candidates)
    {
        [void]$markdown.AppendLine()
        [void]$markdown.AppendLine("## $($candidate.Backend)/$($candidate.Os): ``$($candidate.ScenarioPair)``")
        Add-DiagnosticSection -Builder $markdown -Heading 'Current-source diagnostic evidence' -Diagnostic $candidate.currentDiagnostic
        Add-DiagnosticSection -Builder $markdown -Heading 'Last-healthy-source diagnostic evidence' -Diagnostic $candidate.healthyDiagnostic
    }

    $markdown.ToString() | Set-Content -LiteralPath $markdownPath -Encoding utf8NoBOM
}

Export-ModuleMember -Function @(
    'Write-RegressionDetectionReport',
    'Write-ActualRunEvidenceReport',
    'Write-DiagnosticEvidenceReport'
)
