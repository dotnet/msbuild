# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackagesPath,
    [Parameter(Mandatory = $true)]
    [string] $ClusterUri,
    [Parameter(Mandatory = $true)]
    [string] $Database,
    [Parameter(Mandatory = $true)]
    [string] $QueryFunction,
    [ValidateRange(1, 180)]
    [int] $TimeoutMinutes = 60,
    [ValidateRange(5, 300)]
    [int] $PollIntervalSeconds = 30,
    [ValidateSet('true', 'false')]
    [string] $AllowFailure = 'false'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

function Get-KustoToken {
    $token = & az account get-access-token --resource https://kusto.kusto.windows.net `
        --query accessToken --output tsv --only-show-errors
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) {
        throw 'Unable to acquire an Azure Data Explorer access token.'
    }

    return $token.Trim()
}

foreach ($setting in @{
        PackagesPath = $PackagesPath
        TelemetryCanaryClusterUri = $ClusterUri
        TelemetryCanaryDatabase = $Database
        TelemetryCanaryQueryFunction = $QueryFunction
    }.GetEnumerator()) {
    if ([string]::IsNullOrWhiteSpace($setting.Value) -or $setting.Value -match '^\$\(.+\)$') {
        throw "Required pipeline setting '$($setting.Key)' is not configured."
    }
}

$parsedClusterUri = $null
if (-not [Uri]::TryCreate($ClusterUri, [UriKind]::Absolute, [ref]$parsedClusterUri) -or
    $parsedClusterUri.Scheme -ne 'https' -or
    $parsedClusterUri.Host -notmatch '\.kusto\.windows\.net$' -or
    $parsedClusterUri.AbsolutePath -ne '/' -or
    $parsedClusterUri.Query) {
    throw 'TelemetryCanaryClusterUri must be an Azure Data Explorer cluster root under kusto.windows.net.'
}
if ($QueryFunction -notmatch '^[A-Za-z_][A-Za-z0-9_.]*$') {
    throw 'TelemetryCanaryQueryFunction contains unsupported characters.'
}

$runtimePackages = @(Get-ChildItem -LiteralPath $PackagesPath `
        -Filter 'Microsoft.Build.Runtime.*.nupkg' -File -Recurse)
if ($runtimePackages.Count -ne 1) {
    throw "Expected exactly one Microsoft.Build.Runtime package; found $($runtimePackages.Count)."
}

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) "msbuild-telemetry-canary-$([Guid]::NewGuid().ToString('N'))"
New-Item -Path $temporaryRoot -ItemType Directory | Out-Null

try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($runtimePackages[0].FullName, $temporaryRoot)
    $msbuildPath = Join-Path $temporaryRoot 'contentFiles\any\net472\MSBuild.exe'
    if (-not (Test-Path -LiteralPath $msbuildPath -PathType Leaf)) {
        throw 'The runtime package does not contain the full-framework MSBuild executable.'
    }

    $canaryId = [Guid]::NewGuid().ToString('N')
    $windowStart = [DateTime]::UtcNow.AddMinutes(-5)
    $previousCanaryId = $env:MSBUILD_TELEMETRY_CANARY_ID
    $previousOptOut = $env:MSBUILD_TELEMETRY_OPTOUT
    try {
        $env:MSBUILD_TELEMETRY_CANARY_ID = $canaryId
        Remove-Item Env:MSBUILD_TELEMETRY_OPTOUT -ErrorAction SilentlyContinue
        $versionOutput = @(& $msbuildPath -nologo -version 2>&1 | ForEach-Object { "$_" })
        $candidateExitCode = $LASTEXITCODE
    }
    finally {
        $env:MSBUILD_TELEMETRY_CANARY_ID = $previousCanaryId
        $env:MSBUILD_TELEMETRY_OPTOUT = $previousOptOut
    }

    if ($candidateExitCode -ne 0) {
        throw "Candidate MSBuild exited with code $candidateExitCode.`n$($versionOutput -join [Environment]::NewLine)"
    }

    $reportedVersion = $versionOutput |
        Where-Object { $_ -match '^\s*\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?\s*$' } |
        Select-Object -Last 1
    if ([string]::IsNullOrWhiteSpace($reportedVersion)) {
        throw "Candidate MSBuild did not report a recognizable version.`n$($versionOutput -join [Environment]::NewLine)"
    }
    $reportedVersion = $reportedVersion.Trim()

    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    $token = Get-KustoToken
    $tokenAcquiredAt = [DateTime]::UtcNow
    $lastQueryError = $null
    $attempt = 0

    Write-Host "Candidate package: $($runtimePackages[0].Name)"
    Write-Host "Candidate version: $reportedVersion"
    Write-Host "Expected event: VS/MSBuild/ReleaseCanary"
    Write-Host "Canary id: $canaryId"
    Write-Host "Query window: $($windowStart.ToString('o')) through $($deadline.ToString('o'))"

    while ([DateTime]::UtcNow -lt $deadline) {
        $attempt++
        if (([DateTime]::UtcNow - $tokenAcquiredAt).TotalMinutes -ge 40) {
            $token = Get-KustoToken
            $tokenAcquiredAt = [DateTime]::UtcNow
        }

        $escapedVersion = $reportedVersion.Replace("'", "''")
        $windowEnd = [DateTime]::UtcNow
        $query = "$QueryFunction('$canaryId', '$escapedVersion', datetime($($windowStart.ToString('o'))), datetime($($windowEnd.ToString('o')))) | count"
        try {
            $response = Invoke-RestMethod -Method Post `
                -Uri "$($ClusterUri.TrimEnd('/'))/v1/rest/query" `
                -Headers @{
                    Authorization = "Bearer $token"
                    'x-ms-app' = 'MSBuild release telemetry canary'
                    'x-ms-client-request-id' = "MSBuildReleaseCanary;$canaryId;$attempt"
                } `
                -ContentType 'application/json; charset=utf-8' `
                -Body (@{ db = $Database; csl = $query } | ConvertTo-Json -Compress)

            $lastQueryError = $null
            if ([long]$response.Tables[0].Rows[0][0] -gt 0) {
                Write-Host "Observed release telemetry canary after $attempt query attempt(s)."
                return
            }
        }
        catch {
            $lastQueryError = $_.Exception.Message
            Write-Warning "Telemetry query attempt $attempt failed: $lastQueryError"
        }

        Start-Sleep -Seconds $PollIntervalSeconds
    }

    $failureMessage = "MSBuild release telemetry canary was not observed.`n" +
        "Candidate version: $reportedVersion`nExpected event: VS/MSBuild/ReleaseCanary`n" +
        "Canary id: $canaryId`nQuery window: $($windowStart.ToString('o')) through $([DateTime]::UtcNow.ToString('o'))`n" +
        "Last query error: $lastQueryError"
    if ($AllowFailure -eq 'true') {
        Write-Warning $failureMessage
        Write-Warning 'Telemetry canary failure was explicitly overridden for this manual pipeline run.'
        return
    }

    throw $failureMessage
}
finally {
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force -ErrorAction SilentlyContinue
}
