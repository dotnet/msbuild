# Copyright (c) Microsoft. All rights reserved.

Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'HttpRetry.psm1')

function New-GitHubClient
{
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)][string]$AccessToken
    )

    if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$')
    {
        throw "GitHub repository '$Repository' is not in owner/name format."
    }

    if ([string]::IsNullOrWhiteSpace($AccessToken))
    {
        throw 'A GitHub access token is required.'
    }

    $segments = $Repository.Split('/')
    [pscustomobject][ordered]@{
        IssuesUri = "https://api.github.com/repos/$([Uri]::EscapeDataString($segments[0]))/$([Uri]::EscapeDataString($segments[1]))/issues"
        Headers = @{
            Accept = 'application/vnd.github+json'
            Authorization = "Bearer $AccessToken"
            'X-GitHub-Api-Version' = '2022-11-28'
            'User-Agent' = 'dotnet-msbuild-mt-regression'
        }
    }
}

function Invoke-GitHubJson
{
    [OutputType([object])]
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)][string]$Uri
    )

    for ($attempt = 1; $attempt -le 4; $attempt++)
    {
        try
        {
            return Invoke-RestMethod -Method Get -Uri $Uri -Headers $Client.Headers
        }
        catch
        {
            $statusCode = Get-HttpExceptionStatusCode -Exception $_.Exception
            $retryable = Test-RetryableHttpStatusCode -StatusCode $statusCode
            if (-not $retryable -or $attempt -eq 4)
            {
                throw
            }

            Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
        }
    }
}

function Get-GitHubOpenItemsByCreator
{
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)][string]$Creator,
        [string[]]$Labels = @(),
        [ValidateRange(1, 100)][int]$PageSize = 100,
        [ValidateRange(1, 100)][int]$MaximumPages = 20
    )

    $items = [System.Collections.Generic.List[object]]::new()
    $escapedCreator = [Uri]::EscapeDataString($Creator)
    $labelsQuery = if ($Labels.Count -gt 0)
    {
        "&labels=$([Uri]::EscapeDataString($Labels -join ','))"
    }
    else
    {
        ''
    }

    for ($page = 1; $page -le $MaximumPages; $page++)
    {
        # The issues collection includes pull requests but never includes issue comments,
        # review bodies, or review comments.
        $uri = "$($Client.IssuesUri)?state=open&creator=$escapedCreator$labelsQuery&per_page=$PageSize&page=$page"
        $pageItems = @(Invoke-GitHubJson -Client $Client -Uri $uri)
        foreach ($item in $pageItems)
        {
            $items.Add($item)
        }

        if ($pageItems.Count -lt $PageSize)
        {
            return $items.ToArray()
        }
    }

    throw "GitHub returned at least $($PageSize * $MaximumPages) open items for '$Creator'; refusing to use a truncated result."
}

Export-ModuleMember -Function @(
    'New-GitHubClient',
    'Get-GitHubOpenItemsByCreator'
)
