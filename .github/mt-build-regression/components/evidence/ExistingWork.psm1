# Copyright (c) Microsoft. All rights reserved.

Set-StrictMode -Version Latest

$script:TrustedAuthor = 'github-actions[bot]'
$script:WorkflowMarker = '<!-- gh-aw-workflow-id: mt-build-regression.agent -->'
$script:TitlePrefix = '[PerfStar MT Regression] '
$script:RequiredLabels = @('Area: PerfStar', 'Area: Performance', 'automation')

function Get-ExistingWorkProperty
{
    param(
        $InputObject,
        [Parameter(Mandatory)][string]$Name
    )

    if ($null -eq $InputObject)
    {
        return $null
    }

    if ($InputObject -is [System.Collections.IDictionary])
    {
        if ($InputObject.Contains($Name))
        {
            return $InputObject[$Name]
        }

        return $null
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -ne $property)
    {
        return $property.Value
    }

    $null
}

function Test-TrustedExistingWorkItem
{
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]$Item,
        [Parameter(Mandatory)][string]$CandidateSetKey,
        [Parameter(Mandatory)][string]$Repository
    )

    if ($CandidateSetKey -notmatch '^[0-9a-f]{16}$' -or
        $Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')
    {
        return $false
    }

    $state = [string](Get-ExistingWorkProperty -InputObject $Item -Name 'state')
    $author = Get-ExistingWorkProperty -InputObject $Item -Name 'user'
    $authorLogin = [string](Get-ExistingWorkProperty -InputObject $author -Name 'login')
    $title = [string](Get-ExistingWorkProperty -InputObject $Item -Name 'title')
    $body = [string](Get-ExistingWorkProperty -InputObject $Item -Name 'body')
    if ($state -cne 'open' -or
        $authorLogin -cne $script:TrustedAuthor -or
        -not $title.StartsWith($script:TitlePrefix, [StringComparison]::Ordinal) -or
        -not $body.Contains($script:WorkflowMarker, [StringComparison]::Ordinal))
    {
        return $false
    }

    $labelNames = @(
        foreach ($label in @(Get-ExistingWorkProperty -InputObject $Item -Name 'labels'))
        {
            [string](Get-ExistingWorkProperty -InputObject $label -Name 'name')
        })
    foreach ($requiredLabel in $script:RequiredLabels)
    {
        if ($requiredLabel -cnotin $labelNames)
        {
            return $false
        }
    }

    $candidateMarker = "(?m)^\s*perfstar-mt-regression-key:\s*$([regex]::Escape($CandidateSetKey))\s*$"
    if ($body -cnotmatch $candidateMarker)
    {
        return $false
    }

    $number = 0
    if (-not [int]::TryParse(
        [string](Get-ExistingWorkProperty -InputObject $Item -Name 'number'),
        [ref]$number) -or
        $number -le 0)
    {
        return $false
    }

    $url = $null
    if (-not [Uri]::TryCreate(
        [string](Get-ExistingWorkProperty -InputObject $Item -Name 'html_url'),
        [UriKind]::Absolute,
        [ref]$url) -or
        $url.Scheme -cne 'https' -or
        $url.Host -cne 'github.com')
    {
        return $false
    }

    $isPullRequest = $null -ne (Get-ExistingWorkProperty -InputObject $Item -Name 'pull_request')
    $kind = $isPullRequest ? 'pull' : 'issues'
    $expectedPath = "/$Repository/$kind/$number"
    $url.AbsolutePath.Equals($expectedPath, [StringComparison]::Ordinal)
}

function New-ExistingWorkReport
{
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][object[]]$Items,
        [Parameter(Mandatory)][string]$CandidateSetKey,
        [Parameter(Mandatory)][string]$Repository
    )

    $trustedItems = @(
        foreach ($item in $Items)
        {
            if (-not (Test-TrustedExistingWorkItem `
                -Item $item `
                -CandidateSetKey $CandidateSetKey `
                -Repository $Repository))
            {
                continue
            }

            $isPullRequest = $null -ne (Get-ExistingWorkProperty -InputObject $item -Name 'pull_request')
            [pscustomobject][ordered]@{
                type = $isPullRequest ? 'pull_request' : 'issue'
                number = [int](Get-ExistingWorkProperty -InputObject $item -Name 'number')
                url = [string](Get-ExistingWorkProperty -InputObject $item -Name 'html_url')
            }
        })

    $trustedItems = @($trustedItems | Sort-Object type, number -Unique)
    [ordered]@{
        schemaVersion = 1
        candidateSetKey = $CandidateSetKey
        alreadyTracked = $trustedItems.Count -gt 0
        items = $trustedItems
    }
}

function Write-ExistingWorkReport
{
    param(
        [Parameter(Mandatory)]$Report,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
    $path = Join-Path $OutputDirectory 'mt-regression-existing-work.json'
    $Report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $path -Encoding utf8NoBOM
}

Export-ModuleMember -Function @(
    'Test-TrustedExistingWorkItem',
    'New-ExistingWorkReport',
    'Write-ExistingWorkReport'
)
