#requires -Version 5.1
<#
End-to-end build cost matrix.

Baseline for every measurement: MSBuild Server (on by default in the daily SDK) plus -mt.

Scenarios
  cold-noop      server shut down, outputs present, nothing changed
  warm-noop      server warm,      outputs present, nothing changed
  warm-inc       server warm,      outputs present, one leaf source file touched
  warm-clean     server warm,      bin/obj deleted and restored untimed, then built
  cold-clean     server shut down, bin/obj deleted and restored untimed, then built

"cold" means no MSBuild process state (server and nodes shut down). The operating system file
cache is warm in every case; this measures MSBuild's own cold start, not a cold machine.
#>
param(
    [Parameter(Mandatory)][string]$Name,
    [Parameter(Mandatory)][string]$ProjectOrSln,
    [Parameter(Mandatory)][string]$TouchFile,
    [string[]]$CleanRoots,
    [int]$CheapReps = 5,
    [int]$ExpensiveReps = 3,
    [switch]$SkipClean,
    [string]$Dotnet = 'C:\dotnet-daily\dotnet.exe',
    [string]$OutDir = 'C:\bench\results'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

New-Item -ItemType Directory -Force -Path $OutDir *> $null

$buildArgs = @('build', $ProjectOrSln, '--no-restore', '-v:q', '--nologo', '-mt')

function Stop-Servers {
    & $Dotnet build-server shutdown *> $null
    Start-Sleep -Milliseconds 900
    $procs = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='MSBuild.exe'" |
               Where-Object { $_.CommandLine -match 'nodemode' })
    foreach ($p in $procs) { try { Stop-Process -Id $p.ProcessId -Force -EA Stop } catch {} }
    Start-Sleep -Milliseconds 400
}

function Invoke-Timed {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & $Dotnet @buildArgs *> $null
    $sw.Stop()
    return [pscustomobject]@{ Ms = [int]$sw.Elapsed.TotalMilliseconds; Exit = $LASTEXITCODE }
}

function Remove-Outputs {
    foreach ($root in $CleanRoots) {
        Get-ChildItem $root -Include bin, obj -Directory -Recurse -EA 0 |
            ForEach-Object { Remove-Item $_.FullName -Recurse -Force -EA 0 }
    }
}

function Restore-Untimed {
    & $Dotnet restore $ProjectOrSln *> $null
    if ($LASTEXITCODE -ne 0) { throw "restore failed for $ProjectOrSln" }
}

function Med {
    param($v)
    $s = @($v | Sort-Object)
    $n = $s.Count
    if ($n -eq 0) { return 0 }
    # [int](3/2) is 2 in .NET (banker's rounding), which would pick the maximum for odd counts.
    if ($n % 2 -eq 1) { return $s[[int](($n - 1) / 2)] }
    return [int](($s[$n / 2 - 1] + $s[$n / 2]) / 2)
}

function Report {
    param([string]$Scenario, [int[]]$Samples, [int]$Failures)
    $m = Med $samples
    $obj = [pscustomobject]@{
        Subject  = $Name
        Scenario = $Scenario
        Median   = $m
        Min      = ($samples | Measure-Object -Minimum).Minimum
        Max      = ($samples | Measure-Object -Maximum).Maximum
        N        = $samples.Count
        Failures = $Failures
        Raw      = ($samples -join ',')
    }
    Write-Host ("  {0,-12} median={1,7} ms   min={2,7}   max={3,7}   n={4}{5}" -f `
        $Scenario, $obj.Median, $obj.Min, $obj.Max, $obj.N, $(if ($Failures) { "   FAILURES=$Failures" } else { "" }))
    return $obj
}

Write-Host "=== $Name ==="
$results = @()

# Establish a built, up-to-date state.
Restore-Untimed
Stop-Servers
$null = Invoke-Timed
$null = Invoke-Timed

# --- warm no-op -------------------------------------------------------------
$s = @(); $f = 0
for ($i = 0; $i -lt $CheapReps; $i++) { $r = Invoke-Timed; $s += $r.Ms; if ($r.Exit) { $f++ } }
$results += Report 'warm-noop' $s $f

# --- warm incremental -------------------------------------------------------
$s = @(); $f = 0
for ($i = 0; $i -lt $CheapReps; $i++) {
    (Get-Item $TouchFile).LastWriteTime = Get-Date
    Start-Sleep -Milliseconds 60
    $r = Invoke-Timed; $s += $r.Ms; if ($r.Exit) { $f++ }
}
$results += Report 'warm-inc' $s $f

# --- cold no-op -------------------------------------------------------------
$s = @(); $f = 0
for ($i = 0; $i -lt $CheapReps; $i++) { Stop-Servers; $r = Invoke-Timed; $s += $r.Ms; if ($r.Exit) { $f++ } }
$results += Report 'cold-noop' $s $f

if (-not $SkipClean) {
    # --- warm clean ---------------------------------------------------------
    # The server is already warm from the scenarios above; warm it once more against a
    # freshly cleaned tree so the first timed iteration is not the odd one out.
    Remove-Outputs; Restore-Untimed
    $null = Invoke-Timed

    $s = @(); $f = 0
    for ($i = 0; $i -lt $ExpensiveReps; $i++) {
        Remove-Outputs; Restore-Untimed
        $r = Invoke-Timed; $s += $r.Ms; if ($r.Exit) { $f++ }
    }
    $results += Report 'warm-clean' $s $f

    # --- cold clean ---------------------------------------------------------
    $s = @(); $f = 0
    for ($i = 0; $i -lt $ExpensiveReps; $i++) {
        Remove-Outputs; Restore-Untimed; Stop-Servers
        $r = Invoke-Timed; $s += $r.Ms; if ($r.Exit) { $f++ }
    }
    $results += Report 'cold-clean' $s $f
}

$results | Export-Csv -Path (Join-Path $OutDir "$Name.csv") -NoTypeInformation
Write-Host "  -> $(Join-Path $OutDir "$Name.csv")"
Stop-Servers
