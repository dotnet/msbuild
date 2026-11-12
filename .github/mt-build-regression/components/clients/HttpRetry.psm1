# Copyright (c) Microsoft. All rights reserved.

Set-StrictMode -Version Latest

function Get-HttpExceptionStatusCode
{
    [OutputType([int])]
    param([Parameter(Mandatory)][Exception]$Exception)

    # PowerShell can wrap .NET HTTP failures in a method-invocation exception.
    while ($null -ne $Exception)
    {
        $responseProperty = $Exception.PSObject.Properties['Response']
        if ($null -ne $responseProperty -and $null -ne $responseProperty.Value)
        {
            return [int]$responseProperty.Value.StatusCode
        }
        $statusProperty = $Exception.PSObject.Properties['StatusCode']
        if ($null -ne $statusProperty -and $null -ne $statusProperty.Value)
        {
            return [int]$statusProperty.Value
        }
        $Exception = $Exception.InnerException
    }

    0
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
