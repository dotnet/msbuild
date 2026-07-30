#!/usr/bin/env pwsh
# Copyright (c) Microsoft. All rights reserved.

$ErrorActionPreference = 'Stop'

$featureRoot = Split-Path $PSScriptRoot -Parent
Import-Module (Join-Path $featureRoot 'components\evidence\EvidenceSanitizer.psm1') -Force
Import-Module (Join-Path $featureRoot 'components\evidence\RegressionDetection.psm1') -Force
Import-Module (Join-Path $featureRoot 'components\evidence\RunSelection.psm1') -Force
Import-Module (Join-Path $featureRoot 'components\evidence\ActualRunEvidence.psm1') -Force
Import-Module (Join-Path $featureRoot 'components\evidence\DiagnosticEvidence.psm1') -Force
Import-Module (Join-Path $featureRoot 'components\reporting\RegressionReportWriter.psm1') -Force
Import-Module (Join-Path $featureRoot 'components\clients\AzureDevOpsClient.psm1') -Force
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

function Get-KqlLetLiteral
{
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string]$Name
    )

    if ($Text -match "(?m)^let\s+$([regex]::Escape($Name))\s*=\s*([^;]+);")
    {
        return $Matches[1].Trim()
    }

    ''
}

function New-TestArchive
{
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][hashtable]$Entries
    )

    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try
    {
        foreach ($entry in $Entries.GetEnumerator())
        {
            $writer = [IO.StreamWriter]::new($archive.CreateEntry($entry.Key).Open())
            try
            {
                $writer.Write($entry.Value)
            }
            finally
            {
                $writer.Dispose()
            }
        }
    }
    finally
    {
        $archive.Dispose()
    }
}

$candidateA = [pscustomobject]@{ Backend = 'Hosted'; Os = 'Windows'; ScenarioPair = 'Alpha' }
$candidateB = [pscustomobject]@{ Backend = 'Gold'; Os = 'Linux'; ScenarioPair = 'Beta' }
$identity1 = Get-CandidateSetIdentity -Candidates @($candidateA, $candidateB, $candidateA)
$identity2 = Get-CandidateSetIdentity -Candidates @($candidateB, $candidateA)
Assert-Equal $identity1.Key $identity2.Key 'Candidate-set key must ignore order and duplicates.'
Assert-Equal 2 @($identity1.Inputs).Count 'Candidate-set inputs must be unique.'
Assert-Equal 'Gold/Linux/Beta' $identity1.Inputs[0] 'Candidate-set inputs must be sorted.'

$detectorReport = New-RegressionDetectionReport -Candidates @() -GeneratedAtUtc ([DateTimeOffset]::UtcNow)
$detector = $detectorReport.detector
$kql = Get-Content -LiteralPath (Join-Path $featureRoot 'queries\Get-MtBuildTimeRegressions.kql') -Raw
Assert-Equal "$($detector.lookbackDays)d" (Get-KqlLetLiteral -Text $kql -Name 'lookback') 'Detector lookback metadata must match Kusto.'
Assert-Equal "$($detector.freshnessDays)d" (Get-KqlLetLiteral -Text $kql -Name 'freshness') 'Detector freshness metadata must match Kusto.'
Assert-Equal "$($detector.minimumBaselineRuns)" (Get-KqlLetLiteral -Text $kql -Name 'minBaselineRuns') 'Detector baseline-run metadata must match Kusto.'
Assert-Equal ([double]$detector.minimumMtRegressionPercent).ToString('0.0', [Globalization.CultureInfo]::InvariantCulture) (Get-KqlLetLiteral -Text $kql -Name 'minRegressionPercent') 'Detector percentage metadata must match Kusto.'
Assert-Equal ([double]$detector.minimumMtRegressionMs).ToString('0.0', [Globalization.CultureInfo]::InvariantCulture) (Get-KqlLetLiteral -Text $kql -Name 'minRegressionMs') 'Detector millisecond metadata must match Kusto.'
Assert-True ($kql.Contains('| where MtMedianMs > BaselineMtP90Ms')) 'The executable detector must require MT above baseline p90.'
Assert-True ($kql.Contains('MtVsNonMtDeltaMs > BaselineDeltaP90Ms')) 'The executable detector must require differential above baseline p90.'

Assert-True (Test-SafeMetricName -Name 'build-time') 'build-time must be allowlisted.'
Assert-True (Test-SafeMetricName -Name 'evaluation-time-pass3') 'Evaluation pass metrics must be allowlisted.'
Assert-True (-not (Test-SafeMetricName -Name 'secret-environment')) 'Unknown metrics must be rejected.'
Assert-True (-not (Test-SafeMetricName -Name "build-time`n")) 'Metric names with trailing newlines must be rejected.'
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

function New-TestRun
{
    param([string]$State, [string]$Result)

    [pscustomobject]@{ perfStarBuildState = $State; perfStarBuildResult = $Result }
}

Assert-True (Test-PerfStarRunUsable -Run (New-TestRun 'completed' 'succeeded')) 'Completed successful runs must be usable.'
Assert-True (Test-PerfStarRunUsable -Run (New-TestRun 'completed' 'failed')) 'A completed run that failed overall must remain usable.'
Assert-True (-not (Test-PerfStarRunUsable -Run (New-TestRun 'inProgress' ''))) 'In-progress runs must be rejected.'
Assert-True (-not (Test-PerfStarRunUsable -Run (New-TestRun 'canceling' ''))) 'Canceling runs must be rejected.'
Assert-True (-not (Test-PerfStarRunUsable -Run (New-TestRun 'completed' 'canceled'))) 'Canceled runs report state completed and must still be rejected.'
Assert-True (-not (Test-PerfStarRunUsable -Run (New-TestRun 'completed' 'unknown'))) 'Runs with an unknown result must be rejected.'
Assert-True (-not (Test-PerfStarRunUsable -Run (New-TestRun 'completed' ''))) 'Runs with a missing result must be rejected.'
Assert-True (-not (Test-PerfStarRunUsable -Run ([pscustomobject]@{}))) 'Runs missing the state and result properties must be rejected.'
Assert-Equal 25429 (Get-PerfStarPipelineDefinitionId -Backend 'Gold') 'The Gold backend must map to definition 25429.'
Assert-Equal 28338 (Get-PerfStarPipelineDefinitionId -Backend 'Hosted') 'The Hosted backend must map to definition 28338.'

# Azure DevOps omits properties instead of nulling them: an in-progress run carries no 'result', and
# a run without a component resource carries no 'resources.pipelines'. The resolver runs under
# StrictMode, so it must read those through the property collection rather than dotted access.
$runSelectionModule = Get-Module RunSelection
if ($null -eq $runSelectionModule)
{
    $failures.Add('RunSelection module must be loaded for the payload-shape test.')
}
else
{
    $sparseMetadata = & $runSelectionModule {
        function Get-AzureDevOpsPipelineRun
        {
            param($Client, $DefinitionId, $BuildId)

            # An in-progress run as Azure DevOps actually returns it.
            [pscustomobject]@{ id = 14815927; name = '20260730.3'; state = 'inProgress' }
        }

        function Get-AzureDevOpsBuild
        {
            throw 'The component build must not be requested when no component resource exists.'
        }

        try
        {
            Get-PerfStarRunMetadata `
                -AzureDevOpsClient ([pscustomobject]@{}) `
                -RunCache (New-PerfStarRunCache) `
                -BuildId '14815927' `
                -Backend 'Hosted'
        }
        catch
        {
            $_.Exception.Message
        }
    }

    Assert-True ($sparseMetadata -is [pscustomobject]) "Resolving a run with omitted properties must not throw. Got '$sparseMetadata'."
    if ($sparseMetadata -is [pscustomobject])
    {
        Assert-Equal 'inProgress' $sparseMetadata.perfStarBuildState 'The run state must survive a sparse payload.'
        Assert-Equal '' $sparseMetadata.perfStarBuildResult 'An omitted result must become an empty string.'
        Assert-Equal '' $sparseMetadata.componentSourceVersion 'An omitted component resource must become an empty string.'
        Assert-True (-not (Test-PerfStarRunUsable -Run $sparseMetadata)) 'A sparse in-progress run must be rejected rather than throw.'
    }
}
Assert-True (Test-SafeKustoDimension -Value 'Scenario-1_mt' -Name 'test') 'Expected diagnostic dimensions must be accepted.'
Assert-True (-not (Test-SafeKustoDimension -Value 'Scenario"; drop' -Name 'test' -WarningAction SilentlyContinue)) 'Unsafe Kusto dimensions must be rejected.'
Assert-True (-not (Test-SafeKustoDimension -Value "Scenario`n" -Name 'test' -WarningAction SilentlyContinue)) 'Kusto dimensions with trailing newlines must be rejected.'
$dimensionWarnings = @()
[void](Test-SafeKustoDimension `
    -Value "Scenario`n::error::forged" `
    -Name 'scenario pair' `
    -WarningAction SilentlyContinue `
    -WarningVariable dimensionWarnings)
Assert-True (-not (($dimensionWarnings -join "`n").Contains('forged'))) 'Unsafe Kusto values must not be echoed in warnings.'

Assert-True (Test-AzureDevOpsArtifactUrl -Url 'https://dev.azure.com/devdiv/artifact') 'Azure DevOps artifact URLs must be accepted.'
Assert-True (Test-AzureDevOpsArtifactUrl -Url 'https://artprodcus3.artifacts.visualstudio.com/content') 'Azure artifact-service URLs must be accepted.'
Assert-True (-not (Test-AzureDevOpsArtifactUrl -Url 'http://dev.azure.com/devdiv')) 'Non-HTTPS artifact URLs must be rejected.'
Assert-True (-not (Test-AzureDevOpsArtifactUrl -Url 'https://dev.azure.com.evil.example/artifact')) 'Suffix-confusion artifact hosts must be rejected.'
Assert-True (-not (Test-AzureDevOpsArtifactUrl -Url 'https://dev.azure.com@evil.example/artifact')) 'Artifact URLs with misleading user-info must be rejected.'

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

    $archivePath = Join-Path $tempRoot 'safe.zip'
    New-TestArchive -Path $archivePath -Entries @{
        'a.txt' = 'aa'
        'sub/b.txt' = 'b'
    }
    $safeExtract = Join-Path $tempRoot 'safe-extract'
    Assert-True (Expand-BoundedArchive -ArchivePath $archivePath -DestinationPath $safeExtract) 'A bounded safe archive must extract.'
    Assert-True (Test-Path -LiteralPath (Join-Path (Join-Path $safeExtract 'sub') 'b.txt')) 'Safe archive contents must be present.'
    Assert-True (-not (Expand-BoundedArchive -ArchivePath $archivePath -DestinationPath (Join-Path $tempRoot 'entry-bound') -MaximumEntryCount 1 -WarningAction SilentlyContinue)) 'The entry-count bound must be enforced.'
    Assert-True (-not (Expand-BoundedArchive -ArchivePath $archivePath -DestinationPath (Join-Path $tempRoot 'expanded-bound') -MaximumUncompressedBytes 1 -WarningAction SilentlyContinue)) 'The uncompressed-size bound must be enforced.'
    Assert-True (-not (Expand-BoundedArchive -ArchivePath $archivePath -DestinationPath (Join-Path $tempRoot 'compressed-bound') -MaximumCompressedBytes 1 -WarningAction SilentlyContinue)) 'The compressed-size bound must be enforced.'

    $traversalArchive = Join-Path $tempRoot 'traversal.zip'
    New-TestArchive -Path $traversalArchive -Entries @{ '../escape.txt' = 'escape' }
    $traversalWarnings = @()
    Assert-True (-not (Expand-BoundedArchive `
        -ArchivePath $traversalArchive `
        -DestinationPath (Join-Path $tempRoot 'traversal') `
        -WarningAction SilentlyContinue `
        -WarningVariable traversalWarnings)) 'Traversal entries must be rejected.'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $tempRoot 'escape.txt'))) 'Traversal entries must not escape the destination.'
    Assert-True (-not (($traversalWarnings -join "`n").Contains('escape.txt'))) 'Archive entry names must not be echoed in warnings.'

    # The evidence step must fail loudly rather than silently drop a candidate the detector accepted,
    # and it must not touch artifacts for a run it rejects. Shadow the module's private resolution and
    # artifact functions inside its own scope so this stays offline.
    $actualRunEvidenceModule = Get-Module ActualRunEvidence
    if ($null -eq $actualRunEvidenceModule)
    {
        $failures.Add('ActualRunEvidence module must be loaded for the invariant test.')
    }
    else
    {
        $unusableReport = [pscustomobject]@{
            candidates = @([pscustomobject]@{
                Backend = 'Hosted'
                Os = 'Windows'
                ScenarioPair = 'Alpha'
                CurrentBuildId = '1'
                HealthyBuildId = ''
                MtScenario = 'alpha-mt'
                NonMtScenario = 'alpha'
            })
        }

        $invariantResult = & $actualRunEvidenceModule {
            param($Report, $RawDirectory)

            function Get-PerfStarRunMetadata
            {
                param($AzureDevOpsClient, $RunCache, $BuildId, $Backend)

                [pscustomobject]@{
                    perfStarBuildId = $BuildId
                    perfStarBuildNumber = '20260730.3'
                    perfStarBuildState = 'inProgress'
                    perfStarBuildResult = ''
                }
            }

            function Get-ScenarioEvidence
            {
                throw 'Artifact evidence must not be requested for an unusable run.'
            }

            try
            {
                [void](Get-ActualRunEvidenceCandidates `
                    -Report $Report `
                    -AzureDevOpsClient ([pscustomobject]@{}) `
                    -RawDirectory $RawDirectory)
                'no-throw'
            }
            catch
            {
                $_.Exception.Message
            }
        } $unusableReport (Join-Path $tempRoot 'unusable-run')

        Assert-True ($invariantResult -like '*is not usable*') "The evidence step must reject a candidate whose current run is unusable. Got '$invariantResult'."
        Assert-True ($invariantResult -notlike '*must not be requested*') 'The evidence step must reject an unusable run before requesting artifacts.'
    }

    $reportDirectory = Join-Path $tempRoot 'reports'
    $report = New-RegressionDetectionReport -Candidates @() -GeneratedAtUtc ([DateTimeOffset]::Parse('2026-01-01T00:00:00Z'))
    $report.detector.lookbackDays = 33
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
    $contextMarkdown = Get-Content -LiteralPath (Join-Path $reportDirectory 'mt-regression-context.md') -Raw
    Assert-True ($contextMarkdown.Contains('preceding 33 days')) 'Markdown thresholds must render from detector metadata.'
}
finally
{
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

# Each workflows/*.ps1 entry point must be able to resolve every component command it calls after
# running only its own Import-Module statements. A nested `Import-Module -Force` in a component
# module silently unbinds a client module from the entry script's scope, so this is verified in a
# fresh runspace per entry point rather than in this script's already-populated session.
$workflowRoot = Join-Path $featureRoot 'workflows'
$componentCommands = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($moduleFile in Get-ChildItem -LiteralPath (Join-Path $featureRoot 'components') -Recurse -Filter '*.psm1')
{
    $moduleTokens = $null
    $moduleErrors = $null
    $moduleAst = [System.Management.Automation.Language.Parser]::ParseFile($moduleFile.FullName, [ref]$moduleTokens, [ref]$moduleErrors)
    foreach ($definition in $moduleAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $true))
    {
        [void]$componentCommands.Add($definition.Name)
    }
}

foreach ($entryPoint in Get-ChildItem -LiteralPath $workflowRoot -Filter '*.ps1')
{
    $entryTokens = $null
    $entryErrors = $null
    $entryAst = [System.Management.Automation.Language.Parser]::ParseFile($entryPoint.FullName, [ref]$entryTokens, [ref]$entryErrors)

    $importStatements = @(
        $entryAst.FindAll({
            param($node)
            $node -is [System.Management.Automation.Language.CommandAst] -and
            $node.GetCommandName() -eq 'Import-Module'
        }, $true) | ForEach-Object { $_.Extent.Text })

    $invokedCommands = @(
        $entryAst.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] }, $true) |
            ForEach-Object { $_.GetCommandName() } |
            Where-Object { $_ -and $componentCommands.Contains($_) } |
            Sort-Object -Unique)

    Assert-True ($invokedCommands.Count -gt 0) "$($entryPoint.Name) must call at least one component command."

    $probe = [powershell]::Create()
    try
    {
        [void]$probe.AddScript(@"
`$PSScriptRoot = '$($workflowRoot -replace "'", "''")'
$($importStatements -join "`n")
@($($(($invokedCommands | ForEach-Object { "'$_'" }) -join ',')))|
    Where-Object { -not (Get-Command `$_ -ErrorAction SilentlyContinue) }
"@)
        $unresolved = @($probe.Invoke())
        foreach ($probeError in $probe.Streams.Error)
        {
            $failures.Add("$($entryPoint.Name) failed to import its modules: $probeError")
        }

        Assert-Equal 0 $unresolved.Count "$($entryPoint.Name) cannot resolve component command(s): $($unresolved -join ', ')."
    }
    finally
    {
        $probe.Dispose()
    }
}

if ($failures.Count -gt 0)
{
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "$($failures.Count) MT regression test(s) failed."
}

Write-Host 'All MT regression component tests passed.'
