# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\validate-telemetry-canary.ps1" `
    -PackagesPath unused `
    -ClusterUri https://unused.kusto.windows.net `
    -Database unused `
    -QueryFunction unused `
    -ImportFunctions

function Assert-Equal($Expected, $Actual, [string] $Message) {
    if ($Expected -ne $Actual) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

Assert-Equal '18.11.0.41801' `
    (Get-MSBuildReportedVersion @('18.11.0.41801')) `
    'Four-component MSBuild version parsing failed.'

$row = [object[]]@(2)
$table = [pscustomobject]@{ Rows = [object[]]@(,$row) }
$response = [pscustomobject]@{ Tables = [object[]]@($table) }
Assert-Equal 2 (Get-CanaryCount $response) 'Kusto count parsing failed.'

$state = @{ Calls = 0 }
$observed = Wait-ForCanary -DeadlineUtc ([DateTime]::UtcNow.AddSeconds(1)) -PollIntervalSeconds 0 -Query {
    $state.Calls++
    return $true
}
Assert-Equal $true $observed 'Successful query was not observed.'
Assert-Equal 1 $state.Calls 'Successful query was retried.'

$state.Calls = 0
$observed = Wait-ForCanary -DeadlineUtc ([DateTime]::UtcNow.AddSeconds(-1)) -PollIntervalSeconds 0 -Query {
    $state.Calls++
    return $true
}
Assert-Equal $false $observed 'Expired polling deadline did not time out.'
Assert-Equal 0 $state.Calls 'Query ran after the polling deadline.'

$pipeline = Get-Content "$PSScriptRoot\..\.vsts-dotnet.yml" -Raw
foreach ($expectedText in @(
        '${{ if eq(parameters.allowTelemetryCanaryFailure, false) }}:',
        '${{ if eq(parameters.allowTelemetryCanaryFailure, true) }}:',
        'azureSubscription: ${{ parameters.telemetryCanaryAzureServiceConnection }}'
    )) {
    if (-not $pipeline.Contains($expectedText)) {
        throw "Pipeline override test did not find '$expectedText'."
    }
}

Write-Host 'Telemetry canary validator tests passed.'
