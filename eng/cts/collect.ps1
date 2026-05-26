<#
.SYNOPSIS
    Collect a Clever Test Selection (CTS) baseline for the Debug configuration.

.DESCRIPTION
    Runs all tests in artifacts/bin (Debug) once to build the CTS baseline.
    The baseline is stored on the local filesystem under artifacts/cts/baseline.

.PARAMETER Tag
    Label for this baseline (e.g. a commit SHA or branch name).

.EXAMPLE
    .\eng\cts\collect.ps1 -Tag main
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

New-Item -ItemType Directory -Force -Path $baselineDir, $logsDir | Out-Null

& cts collect testingplatform `
    --rootPath $repoRoot `
    --config $config `
    --storage-type filesystem `
    --storage-type-filesystem-dir $baselineDir `
    --tag $Tag `
    --logs-directory $logsDir `
    --report-trx

exit $LASTEXITCODE
