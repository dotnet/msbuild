# Copyright (c) Microsoft. All rights reserved.

Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'HttpRetry.psm1') -Force

function New-AzureDevOpsClient
{
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][string]$OrganizationUri,
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$AccessToken
    )

    [pscustomobject][ordered]@{
        ProjectBaseUri = "$($OrganizationUri.TrimEnd('/'))/$([Uri]::EscapeDataString($Project))"
        Headers = @{ Authorization = "Bearer $AccessToken" }
    }
}

function Invoke-AzureDevOpsJson
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

function Get-AzureDevOpsBuild
{
    [OutputType([object])]
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)][string]$BuildId
    )

    $uri = "$($Client.ProjectBaseUri)/_apis/build/builds/$([Uri]::EscapeDataString($BuildId))?api-version=7.1"
    Invoke-AzureDevOpsJson -Client $Client -Uri $uri
}

function Get-AzureDevOpsPipelineRun
{
    [OutputType([object])]
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)][int]$DefinitionId,
        [Parameter(Mandatory)][string]$BuildId
    )

    $uri = "$($Client.ProjectBaseUri)/_apis/pipelines/$DefinitionId/runs/$([Uri]::EscapeDataString($BuildId))?api-version=7.1"
    Invoke-AzureDevOpsJson -Client $Client -Uri $uri
}

function Get-AzureDevOpsPipelineRuns
{
    [OutputType([object])]
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)][int]$DefinitionId
    )

    $uri = "$($Client.ProjectBaseUri)/_apis/pipelines/$DefinitionId/runs?api-version=7.1"
    Invoke-AzureDevOpsJson -Client $Client -Uri $uri
}

function Get-AzureDevOpsArtifact
{
    [OutputType([object])]
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)][string]$BuildId,
        [Parameter(Mandatory)][string]$ArtifactName
    )

    $escapedArtifactName = [Uri]::EscapeDataString($ArtifactName)
    $uri = "$($Client.ProjectBaseUri)/_apis/build/builds/$([Uri]::EscapeDataString($BuildId))/artifacts?artifactName=$escapedArtifactName&api-version=7.1"

    try
    {
        Invoke-AzureDevOpsJson -Client $Client -Uri $uri
    }
    catch
    {
        if ((Get-HttpExceptionStatusCode -Exception $_.Exception) -eq 404)
        {
            return $null
        }

        throw
    }
}

function Save-AzureDevOpsArtifact
{
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)][string]$DownloadUrl,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    for ($attempt = 1; $attempt -le 4; $attempt++)
    {
        try
        {
            Invoke-WebRequest -Uri $DownloadUrl -Headers $Client.Headers -OutFile $DestinationPath
            return
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

Export-ModuleMember -Function @(
    'New-AzureDevOpsClient',
    'Get-AzureDevOpsBuild',
    'Get-AzureDevOpsPipelineRun',
    'Get-AzureDevOpsPipelineRuns',
    'Get-AzureDevOpsArtifact',
    'Save-AzureDevOpsArtifact'
)
