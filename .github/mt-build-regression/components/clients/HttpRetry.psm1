# Copyright (c) Microsoft. All rights reserved.

Set-StrictMode -Version Latest

function Get-HttpExceptionStatusCode
{
    [OutputType([int])]
    param([Parameter(Mandatory)][Exception]$Exception)

    # Transport exceptions have no Response property. Preserve the original status 0 retry path
    # without triggering StrictMode's missing-property error.
    $responseProperty = $Exception.PSObject.Properties['Response']
    if ($null -eq $responseProperty -or $null -eq $responseProperty.Value)
    {
        return 0
    }

    [int]$responseProperty.Value.StatusCode
}

function Test-RetryableHttpStatusCode
{
    [OutputType([bool])]
    param([Parameter(Mandatory)][int]$StatusCode)

    $StatusCode -in @(0, 408, 429, 500, 502, 503, 504)
}

Export-ModuleMember -Function @(
    'Get-HttpExceptionStatusCode',
    'Test-RetryableHttpStatusCode'
)
