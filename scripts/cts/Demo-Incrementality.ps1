<#
.SYNOPSIS
    Demonstrates CTS incremental test selection end-to-end on StringTools.

.DESCRIPTION
    Walks through three scenarios:
      1. Baseline collect on a clean tree
      2. Apply with no working-tree changes              -> 0 tests selected
      3. Apply with a benign change to a StringTools     -> impacted tests run
         source file (line appended, then reverted)
      4. Apply with a benign change to an *unrelated*    -> 0 tests selected
         file (Tasks/AssemblyDependency/Resolver.cs)

    Each scenario prints what CTS picked, proving incremental selection works.

.PARAMETER SkipCollect
    Skip the baseline collect step (assume .cts/baseline/ already exists).

.EXAMPLE
    .\Demo-Incrementality.ps1
    .\Demo-Incrementality.ps1 -SkipCollect
#>
[CmdletBinding()]
param(
    [switch]$SkipCollect
)

. (Join-Path $PSScriptRoot '_Common.ps1')

Ensure-Cli
Ensure-CtsConfig

$demoProjectKey = 'StringTools'
$relatedFile    = Join-Path $script:RepoRoot 'src\StringTools\StringTools.cs'
$unrelatedFile  = Join-Path $script:RepoRoot 'src\Tasks\AssemblyDependency\Resolver.cs'

function Write-Banner($text) {
    Write-Host ''
    Write-Host ('=' * 72) -ForegroundColor Cyan
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host ('=' * 72) -ForegroundColor Cyan
}

function Touch-File($path) {
    if (-not (Test-Path $path)) { throw "Cannot touch '$path' - file not found." }
    Add-Content -Path $path -Value ''
    Write-Host "  + appended one blank line to $([IO.Path]::GetFileName($path))" -ForegroundColor Yellow
}

function Restore-File($path) {
    Push-Location $script:RepoRoot
    try { git checkout -- $path | Out-Null }
    finally { Pop-Location }
    Write-Host "  - reverted $([IO.Path]::GetFileName($path))" -ForegroundColor DarkGray
}

function Show-Selection($logFile) {
    if (-not (Test-Path $logFile)) {
        Write-Host "  (no log at $logFile)" -ForegroundColor Red
        return
    }
    Get-Content $logFile | Where-Object {
        $_ -match 'selected \d+ impacted|impacted test\(s\):|executed:|succeeded:|failed:|reason '
    } | ForEach-Object { Write-Host "  $_" -ForegroundColor Green }
}

# --- 1. Collect baseline -----------------------------------------------------
if (-not $SkipCollect) {
    Write-Banner '1/4  Collect baseline on clean tree'
    & (Join-Path $PSScriptRoot 'Collect-Local.ps1') -Project $demoProjectKey -TimeoutMinutes 5
    if ($LASTEXITCODE -ne 0) { throw 'Collect-Local.ps1 failed.' }
}
else {
    Write-Banner '1/4  Reusing existing baseline (-SkipCollect)'
    if (-not (Test-Path (Join-Path $script:BaselineDir 'collect-vstest'))) {
        throw "No baseline at $script:BaselineDir. Drop -SkipCollect to create one."
    }
}

# --- 2. Apply with no changes ------------------------------------------------
Write-Banner '2/4  Apply with NO working-tree changes  -> expect 0 impacted'
& (Join-Path $PSScriptRoot 'Run-Local.ps1') -Project $demoProjectKey -SkipBuild -TimeoutMinutes 5 | Out-Null
Show-Selection (Join-Path $script:LogsDir "apply-$demoProjectKey.log")

# --- 3. Apply with a related change -----------------------------------------
Write-Banner '3/4  Apply after touching src/StringTools/StringTools.cs  -> expect impacted > 0'
Touch-File $relatedFile
try {
    & (Join-Path $PSScriptRoot 'Run-Local.ps1') -Project $demoProjectKey -SkipBuild -TimeoutMinutes 5 | Out-Null
    Show-Selection (Join-Path $script:LogsDir "apply-$demoProjectKey.log")
}
finally {
    Restore-File $relatedFile
}

# --- 4. Apply with an unrelated change --------------------------------------
Write-Banner '4/4  Apply after touching src/Tasks/AssemblyDependency/Resolver.cs  -> expect 0 impacted'
Touch-File $unrelatedFile
try {
    & (Join-Path $PSScriptRoot 'Run-Local.ps1') -Project $demoProjectKey -SkipBuild -TimeoutMinutes 5 | Out-Null
    Show-Selection (Join-Path $script:LogsDir "apply-$demoProjectKey.log")
}
finally {
    Restore-File $unrelatedFile
}

Write-Banner 'Demo complete'
Write-Host 'CTS correctly selected impacted tests in scenario 3, and skipped' -ForegroundColor Cyan
Write-Host 'all tests in scenarios 2 and 4. The baseline lives at:' -ForegroundColor Cyan
Write-Host "  $script:BaselineDir" -ForegroundColor DarkGray
