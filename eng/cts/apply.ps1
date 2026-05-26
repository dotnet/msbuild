<#
.SYNOPSIS
    Apply the CTS baseline to run only the tests affected by your current changes (Debug).

.PARAMETER Tag
    Baseline tag previously created via collect.ps1.

.EXAMPLE
    .\eng\cts\apply.ps1 -Tag main
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Tag
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path "$PSScriptRoot\..\..").Path
$config = Join-Path $repoRoot 'cts.json'
$baselineDir = Join-Path $repoRoot 'artifacts\cts\baseline'
$logsDir = Join-Path $repoRoot 'artifacts\cts\logs'

if (-not (Test-Path $baselineDir)) {
    throw "No baseline found at $baselineDir. Run eng\cts\collect.ps1 first."
}

New-Item -ItemType Directory -Force -Path $logsDir | Out-Null

& cts apply testingplatform `
    --rootPath $repoRoot `
    --config $config `
    --storage-type filesystem `
    --storage-type-filesystem-dir $baselineDir `
    --tag $Tag `
    --logs-directory $logsDir `
    --report-trx

exit $LASTEXITCODE
