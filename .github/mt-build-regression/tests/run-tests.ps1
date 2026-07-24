#!/usr/bin/env pwsh
# Copyright (c) Microsoft. All rights reserved.

$ErrorActionPreference = 'Stop'

$featureRoot = Split-Path $PSScriptRoot -Parent
Import-Module (Join-Path $featureRoot 'components\evidence\EvidenceSanitizer.psm1') -Force
Import-Module (Join-Path $featureRoot 'components\evidence\RegressionDetection.psm1') -Force
Import-Module (Join-Path $featureRoot 'components\evidence\DiagnosticEvidence.psm1') -Force
Import-Module (Join-Path $featureRoot 'components\reporting\RegressionReportWriter.psm1') -Force
Import-Module (Join-Path $featureRoot 'components\clients\HttpRetry.psm1') -Force

$failures = [System.Collections.Generic.List[string]]::new()

function Assert-True
{
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition)
    {
        $failures.Add($Message)
    }
}

function Assert-Equal
{
    param(
        $Expected,
        $Actual,
        [Parameter(Mandatory)][string]$Message
    )

    if ($Expected -ne $Actual)
    {
        $failures.Add("$Message Expected '$Expected', got '$Actual'.")
    }
}

$candidateA = [pscustomobject]@{ Backend = 'Hosted'; Os = 'Windows'; ScenarioPair = 'Alpha' }
$candidateB = [pscustomobject]@{ Backend = 'Gold'; Os = 'Linux'; ScenarioPair = 'Beta' }
$identity1 = Get-CandidateSetIdentity -Candidates @($candidateA, $candidateB, $candidateA)
$identity2 = Get-CandidateSetIdentity -Candidates @($candidateB, $candidateA)
Assert-Equal $identity1.Key $identity2.Key 'Candidate-set key must ignore order and duplicates.'
Assert-Equal 2 @($identity1.Inputs).Count 'Candidate-set inputs must be unique.'
Assert-Equal 'Gold/Linux/Beta' $identity1.Inputs[0] 'Candidate-set inputs must be sorted.'

Assert-True (Test-SafeMetricName -Name 'build-time') 'build-time must be allowlisted.'
Assert-True (Test-SafeMetricName -Name 'evaluation-time-pass3') 'Evaluation pass metrics must be allowlisted.'
Assert-True (-not (Test-SafeMetricName -Name 'secret-environment')) 'Unknown metrics must be rejected.'
Assert-Equal 'build_123_Windows' (Get-SafeFileName -Value 'build/123:Windows') 'Unsafe filename characters must be replaced.'

$transportException = [System.Net.Http.HttpRequestException]::new('Transient transport failure.')
Assert-Equal 0 (Get-HttpExceptionStatusCode -Exception $transportException) 'Transport failures without Response must map to status 0.'
Assert-True (Test-RetryableHttpStatusCode -StatusCode 0) 'Transport failures must remain retryable.'
Assert-True (-not (Test-RetryableHttpStatusCode -StatusCode 401)) 'Authentication failures must not be retried.'

$metricSource = [pscustomobject]@{
    'build-time' = 123
    'dotnet-version' = '10.0.0'
    'unexpected' = 'discard'
    'evaluation-time-details' = [pscustomobject]@{ nested = 'discard' }
}
$metrics = ConvertTo-AllowlistedMetrics -Properties $metricSource.PSObject.Properties
Assert-Equal 2 $metrics.Count 'Only allowlisted scalar metrics must remain.'
Assert-Equal 123 $metrics['build-time'] 'Allowlisted numeric metrics must be preserved.'

$evidence = [pscustomobject]@{
    candidates = @(
        [pscustomobject]@{
            currentRun = [pscustomobject]@{ componentSourceVersion = ('a' * 40) }
            healthyRun = [pscustomobject]@{ componentSourceVersion = ('B' * 40) }
        },
        [pscustomobject]@{
            currentRun = [pscustomobject]@{ componentSourceVersion = ('A' * 40) }
            healthyRun = $null
        })
}
$sourceVersions = Get-RequiredSourceVersions -Evidence $evidence
Assert-Equal 2 $sourceVersions.Count 'Source versions must be valid, unique SHA-1 values.'
Assert-True (Test-SafeKustoDimension -Value 'Scenario-1_mt' -Name 'test') 'Expected diagnostic dimensions must be accepted.'
Assert-True (-not (Test-SafeKustoDimension -Value 'Scenario"; drop' -Name 'test' -WarningAction SilentlyContinue)) 'Unsafe Kusto dimensions must be rejected.'

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) "mt-regression-tests-$([Guid]::NewGuid().ToString('N'))"
try
{
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    $metricsPath = Join-Path $tempRoot 'scenario.metrics.txt'
    @(
        '##METRIC## build-time=1500',
        '##METRIC## unexpected=discard',
        'ordinary log content'
    ) | Set-Content -LiteralPath $metricsPath -Encoding utf8NoBOM
    $hostedMetrics = Read-HostedMetrics -Path $metricsPath
    Assert-Equal 1 $hostedMetrics.Count 'Hosted metric parsing must discard non-allowlisted values.'
    Assert-Equal '1500' $hostedMetrics['build-time'] 'Hosted metric parsing must retain the value.'

    $logPath = Join-Path $tempRoot 'scenario.log'
    @(
        'sensitive arbitrary command line',
        '[heartbeat] running',
        'Build succeeded.',
        'Time Elapsed 00:00:01'
    ) | Set-Content -LiteralPath $logPath -Encoding utf8NoBOM
    $excerpt = Get-HostedLogExcerpt -Path $logPath
    Assert-True ($excerpt.Contains('[heartbeat] running')) 'Safe heartbeat lines must be retained.'
    Assert-True (-not $excerpt.Contains('sensitive arbitrary command line')) 'Arbitrary log lines must be excluded.'

    $reportDirectory = Join-Path $tempRoot 'reports'
    $report = New-RegressionDetectionReport -Candidates @() -GeneratedAtUtc ([DateTimeOffset]::Parse('2026-01-01T00:00:00Z'))
    Write-RegressionDetectionReport -Report $report -OutputDirectory $reportDirectory
    Write-ActualRunEvidenceReport -Candidates @() -OutputDirectory $reportDirectory
    Write-DiagnosticEvidenceReport -Candidates @() -DiagnosticPipelineId 28394 -MaximumRunsToInspect 24 -OutputDirectory $reportDirectory

    $expectedFiles = @(
        'mt-regressions.json',
        'mt-regression-context.md',
        'mt-regression-evidence.json',
        'mt-regression-evidence.md',
        'mt-regression-diagnostics.json',
        'mt-regression-diagnostics.md'
    )
    foreach ($fileName in $expectedFiles)
    {
        Assert-True (Test-Path -LiteralPath (Join-Path $reportDirectory $fileName)) "Expected report '$fileName' was not written."
    }
}
finally
{
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

if ($failures.Count -gt 0)
{
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "$($failures.Count) MT regression test(s) failed."
}

Write-Host 'All MT regression component tests passed.'
