# Copyright (c) Microsoft. All rights reserved.

Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'HttpRetry.psm1')

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

function Test-AzureDevOpsArtifactUrl
{
    [OutputType([bool])]
    param([Parameter(Mandatory)][string]$Url)

    $uri = $null
    if (-not [Uri]::TryCreate($Url, [UriKind]::Absolute, [ref]$uri))
    {
        return $false
    }

    $uri.Scheme -eq 'https' -and
        [string]::IsNullOrEmpty($uri.UserInfo) -and
        ($uri.Host.Equals('dev.azure.com', [StringComparison]::OrdinalIgnoreCase) -or
            $uri.Host.EndsWith('.artifacts.visualstudio.com', [StringComparison]::OrdinalIgnoreCase))
}

function Copy-BoundedArtifactStream
{
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)][IO.Stream]$Source,
        [Parameter(Mandatory)][IO.Stream]$Destination,
        [Parameter(Mandatory)][long]$MaximumBytes,
        [Threading.CancellationToken]$CancellationToken = [Threading.CancellationToken]::None
    )

    $buffer = [byte[]]::new(64KB)
    $totalBytes = 0L
    while ($true)
    {
        $count = [int][Math]::Min($buffer.Length, $MaximumBytes - $totalBytes)
        if ($count -eq 0) { $count = 1 }
        $read = $Source.ReadAsync($buffer, 0, $count, $CancellationToken).GetAwaiter().GetResult()
        if ($read -eq 0)
        {
            return $true
        }
        if ($read -gt $MaximumBytes - $totalBytes)
        {
            return $false
        }

        $Destination.Write($buffer, 0, $read)
        $totalBytes += $read
    }
}

function Save-AzureDevOpsArtifact
{
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)][string]$DownloadUrl,
        [Parameter(Mandatory)][string]$DestinationPath,
        [ValidateRange(1, [long]::MaxValue)][long]$MaximumBytes = 100MB
    )

    if (-not (Test-AzureDevOpsArtifactUrl -Url $DownloadUrl))
    {
        throw 'Artifact download URL is not an approved Azure DevOps endpoint.'
    }

    for ($attempt = 1; $attempt -le 4; $attempt++)
    {
        $handler = [Net.Http.HttpClientHandler]::new()
        $handler.MaxAutomaticRedirections = 5
        $httpClient = [Net.Http.HttpClient]::new($handler)
        $httpClient.Timeout = [Threading.Timeout]::InfiniteTimeSpan
        $deadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(600))
        $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $DownloadUrl)
        $response = $null
        $source = $null
        $destination = $null
        $saved = $false
        try
        {
            foreach ($header in $Client.Headers.GetEnumerator())
            {
                [void]$request.Headers.TryAddWithoutValidation($header.Key, [string]$header.Value)
            }

            # HttpClientHandler clears Authorization on redirects. Do not buffer the response body.
            $response = $httpClient.SendAsync(
                $request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $deadline.Token).GetAwaiter().GetResult()
            [void]$response.EnsureSuccessStatusCode()
            if ($response.Content.Headers.ContentLength -gt $MaximumBytes)
            {
                Write-Warning 'Artifact archive exceeds the download-size limit.'
                return $false
            }

            $source = $response.Content.ReadAsStreamAsync($deadline.Token).GetAwaiter().GetResult()
            $destination = [IO.File]::Open($DestinationPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
            $copied = Copy-BoundedArtifactStream -Source $source -Destination $destination `
                -MaximumBytes $MaximumBytes -CancellationToken $deadline.Token
            if (-not $copied)
            {
                Write-Warning 'Artifact archive exceeds the download-size limit.'
                return $false
            }
            $destination.Flush()
            $saved = $true
            return $true
        }
        catch
        {
            $statusCode = Get-HttpExceptionStatusCode -Exception $_.Exception
            $retryable = Test-RetryableHttpStatusCode -StatusCode $statusCode
            if (-not $retryable -or $attempt -eq 4)
            {
                throw
            }
        }
        finally
        {
            if ($null -ne $destination) { $destination.Dispose() }
            if ($null -ne $source) { $source.Dispose() }
            if ($null -ne $response) { $response.Dispose() }
            $request.Dispose()
            $deadline.Dispose()
            $httpClient.Dispose()
            if (-not $saved -and $null -ne $destination)
            {
                Remove-Item -LiteralPath $DestinationPath -Force -ErrorAction Stop
            }
        }
        Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
    }
}

Export-ModuleMember -Function @(
    'New-AzureDevOpsClient',
    'Get-AzureDevOpsBuild',
    'Get-AzureDevOpsPipelineRun',
    'Get-AzureDevOpsPipelineRuns',
    'Get-AzureDevOpsArtifact',
    'Test-AzureDevOpsArtifactUrl',
    'Save-AzureDevOpsArtifact'
)
