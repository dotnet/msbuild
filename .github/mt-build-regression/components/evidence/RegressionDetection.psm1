# Copyright (c) Microsoft. All rights reserved.

Set-StrictMode -Version Latest

function Get-CandidateSetIdentity
{
    [OutputType([pscustomobject])]
    param([Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Candidates)

    $inputs = @(
        $Candidates |
            ForEach-Object { "$($_.Backend)/$($_.Os)/$($_.ScenarioPair)" } |
            Sort-Object -Unique)
    $text = $inputs -join "`n"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    $key = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes))

    [pscustomobject][ordered]@{
        Key = $key.Substring(0, 16).ToLowerInvariant()
        Inputs = $inputs
    }
}

function New-RegressionDetectionReport
{
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Candidates,
        [Parameter(Mandatory)][DateTimeOffset]$GeneratedAtUtc
    )

    $identity = Get-CandidateSetIdentity -Candidates $Candidates
    [ordered]@{
        generatedAtUtc = $GeneratedAtUtc.ToString('o')
        candidateSetKey = $identity.Key
        candidateKeyInputs = $identity.Inputs
        # Keep this metadata synchronized with the executable Kusto query.
        detector = [ordered]@{
            lookbackDays = 21
            freshnessDays = 2
            minimumBaselineRuns = 4
            minimumMtRegressionPercent = 5.0
            minimumMtRegressionMs = 250.0
            requiresCurrentMtAboveBaselineP90 = $true
            requiresMtVsNonMtDifferentialRegression = $true
        }
        candidateCount = $Candidates.Count
        candidates = $Candidates
    }
}

Export-ModuleMember -Function @(
    'Get-CandidateSetIdentity',
    'New-RegressionDetectionReport'
)
