# Copyright (c) Microsoft. All rights reserved.

Set-StrictMode -Version Latest

Import-Module (Join-Path $PSScriptRoot 'HttpRetry.psm1') -Force

function New-KustoClient
{
    [OutputType([pscustomobject])]
    param(
        [Parameter(Mandatory)][string]$ClusterUri,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$AccessToken
    )

    [pscustomobject][ordered]@{
        QueryUri = "$($ClusterUri.TrimEnd('/'))/v1/rest/query"
        Database = $Database
        Headers = @{ Authorization = "Bearer $AccessToken" }
    }
}

function ConvertFrom-KustoResponse
{
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory)]$Response,
        [bool]$RequirePrimaryTable
    )

    $table = $Response.Tables | Select-Object -First 1
    if ($null -eq $table)
    {
        if ($RequirePrimaryTable)
        {
            throw 'Kusto returned no primary result table.'
        }

        return @()
    }

    $columnNames = @($table.Columns | ForEach-Object { $_.ColumnName })
    @(
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
}

function Invoke-KustoQuery
{
    [OutputType([object[]])]
    param(
        [Parameter(Mandatory)]$Client,
        [Parameter(Mandatory)][string]$Query,
        [ValidateRange(1, 4)][int]$MaximumAttempts = 1,
        [bool]$RequirePrimaryTable = $false
    )

    $payload = @{
        db = $Client.Database
        csl = $Query
    } | ConvertTo-Json -Compress

    $response = $null
    for ($attempt = 1; $attempt -le $MaximumAttempts; $attempt++)
    {
        try
        {
            $response = Invoke-RestMethod `
                -Method Post `
                -Uri $Client.QueryUri `
                -Headers $Client.Headers `
                -ContentType 'application/json' `
                -Body $payload
            break
        }
        catch
        {
            $statusCode = Get-HttpExceptionStatusCode -Exception $_.Exception
            $retryable = Test-RetryableHttpStatusCode -StatusCode $statusCode
            if (-not $retryable -or $attempt -eq $MaximumAttempts)
            {
                throw
            }

            Start-Sleep -Seconds ([Math]::Pow(2, $attempt))
        }
    }

    ConvertFrom-KustoResponse -Response $response -RequirePrimaryTable $RequirePrimaryTable
}

Export-ModuleMember -Function @(
    'New-KustoClient',
    'Invoke-KustoQuery'
)
