#requires -Version 5.1
# Is -mt actually parallel? Compares the -mt baseline against classic multiproc worker
# nodes and against -mt with an explicit node count, on the same subject.
param(
    [Parameter(Mandatory)][string]$ProjectOrSln,
    [string[]]$CleanRoots,
    [int]$Reps = 3,
    [switch]$IncludeClean,
    [string]$Dotnet = 'C:\dotnet-daily\dotnet.exe'
)

$ErrorActionPreference = 'Stop'
$env:DOTNET_NOLOGO = '1'

function Stop-Servers {
    & $Dotnet build-server shutdown *> $null
    Start-Sleep -Milliseconds 900
    $procs = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='MSBuild.exe'" |
               Where-Object { $_.CommandLine -match 'nodemode' })
    foreach ($p in $procs) { try { Stop-Process -Id $p.ProcessId -Force -EA Stop } catch {} }
    Start-Sleep -Milliseconds 400
}

function Node-Summary {
    $n = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe' OR Name='MSBuild.exe'" |
         ForEach-Object { if ($_.CommandLine -match 'nodemode:(\d+)') { $matches[1] } }
    ($n | Group-Object | ForEach-Object { "$($_.Count)x mode$($_.Name)" }) -join ' '
}

function Med {
    param($v)
    $s = @($v | Sort-Object)
    $n = $s.Count
    if ($n -eq 0) { return 0 }
    if ($n % 2 -eq 1) { return $s[[int](($n - 1) / 2)] }
    return [int](($s[$n / 2 - 1] + $s[$n / 2]) / 2)
}

function Remove-Outputs {
    foreach ($root in $CleanRoots) {
        Get-ChildItem $root -Include bin, obj -Directory -Recurse -EA 0 |
            ForEach-Object { Remove-Item $_.FullName -Recurse -Force -EA 0 }
    }
}

$regimes = @(
    @{ Name = '-mt (baseline)';   Args = @('-mt') },
    @{ Name = '-mt -m:8';         Args = @('-mt', '-m:8') },
    @{ Name = 'classic -m:8';     Args = @('-m:8') },
    @{ Name = 'classic default';  Args = @() }
)

Write-Host "### no-op ###"
foreach ($r in $regimes) {
    Stop-Servers
    $argv = @('build', $ProjectOrSln, '--no-restore', '-v:q', '--nologo') + $r.Args
    & $Dotnet @argv *> $null
    & $Dotnet @argv *> $null           # warm
    $nodes = Node-Summary
    $s = @()
    for ($i = 0; $i -lt $Reps; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew(); & $Dotnet @argv *> $null; $sw.Stop()
        $s += [int]$sw.Elapsed.TotalMilliseconds
    }
    Write-Host ("  {0,-18} warm no-op median={1,7} ms   nodes: {2}   raw: {3}" -f $r.Name, (Med $s), $nodes, ($s -join ','))
}

if ($IncludeClean) {
    Write-Host "### clean (warm server) ###"
    foreach ($r in $regimes) {
        Stop-Servers
        $argv = @('build', $ProjectOrSln, '--no-restore', '-v:q', '--nologo') + $r.Args
        Remove-Outputs; & $Dotnet restore $ProjectOrSln *> $null
        & $Dotnet @argv *> $null       # warm the server against a clean tree
        $s = @()
        for ($i = 0; $i -lt $Reps; $i++) {
            Remove-Outputs; & $Dotnet restore $ProjectOrSln *> $null
            $sw = [System.Diagnostics.Stopwatch]::StartNew(); & $Dotnet @argv *> $null; $sw.Stop()
            $s += [int]$sw.Elapsed.TotalMilliseconds
        }
        Write-Host ("  {0,-18} warm clean median={1,7} ms   raw: {2}" -f $r.Name, (Med $s), ($s -join ','))
    }
}

Stop-Servers
