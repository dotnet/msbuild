<#
.SYNOPSIS
Runs concurrent public-repository builds to exercise coordinator priority scheduling.

.DESCRIPTION
Creates isolated git worktrees, optionally restores and warms them, then runs
coordinator scenarios against a public repository such as Roslyn or Aspire:
  - coordinator-priority: normal builds use Normal priority; delayed builds use High.
  - coordinator-all-normal: all builds use Normal priority.
  - no-coordinator: coordinator disabled; every build uses the full local node count.

The script uses this MSBuild checkout's bootstrap dotnet when available so the
benchmark exercises the local coordinator implementation.

.EXAMPLE
pwsh .\scripts\benchmarks\Run-PublicRepoCoordinatorBenchmark.ps1 `
  -RoslynRoot C:\code\roslyn `
  -BuildPath Compilers.slnf `
  -NormalBuildCount 2 `
  -HighBuildCount 1 `
  -Scenarios coordinator-priority

.EXAMPLE
pwsh .\scripts\benchmarks\Run-PublicRepoCoordinatorBenchmark.ps1 `
  -AspireRoot C:\code\aspire `
  -BuildPath Aspire-Core.slnf `
  -NormalBuildCount 2 `
  -HighBuildCount 1 `
  -Scenarios coordinator-priority
#>
[CmdletBinding()]
param(
    [Alias('AspireRoot', 'RoslynRoot')]
    [string]$BenchmarkRoot,
    [string]$BuildPath,
    [string]$RepositoryName,
    [string]$WorkRoot,
    [string]$OutputRoot,
    [string]$DotNetPath,
    [string]$MSBuildDllPath,
    [switch]$UseDotNetBuild,
    [int]$NodeBudget = [Environment]::ProcessorCount,
    [int]$Slice = 4,
    [int]$Reservation = 4,
    [int]$PriorityAgingThreshold = 3,
    [int]$NormalBuildCount = 2,
    [int]$HighBuildCount = 1,
    [int]$HighDelaySeconds = 10,
    [int]$Rounds = 1,
    [string]$TouchFileRelativePath,
    [string[]]$Scenarios = @('coordinator-priority', 'coordinator-all-normal', 'no-coordinator'),
    [switch]$Prepare,
    [switch]$IncludeRestore,
    [switch]$SkipWarm,
    [switch]$ShowInstructions
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$IsWindowsPlatform = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)
$ProcessorCount = [Environment]::ProcessorCount
$CoordinatorEnvironmentVariables = @(
    'MSBUILDUSECOORDINATOR',
    'MSBUILDCOORDINATORPIPENAME',
    'MSBUILDCOORDINATORNODEBUDGET',
    'MSBUILDCOORDINATORHIGHPRIORITYRESERVEDNODES',
    'MSBUILDCOORDINATORMAXNODESPERBUILD',
    'MSBUILDCOORDINATORPRIORITYAGINGTHRESHOLD',
    'MSBUILDCOORDINATORBUILDREQUESTPRIORITY'
)

function Write-Instructions {
    @"
Public repository coordinator priority benchmark

Prerequisites:
  1. Build this MSBuild checkout: .\build.cmd -v quiet
  2. Have a local public repo checkout, such as Roslyn or Aspire.

Roslyn quick smoke:
  pwsh .\scripts\benchmarks\Run-PublicRepoCoordinatorBenchmark.ps1 -RoslynRoot C:\code\roslyn -BuildPath Compilers.slnf -NormalBuildCount 2 -HighBuildCount 1 -Rounds 1 -Scenarios coordinator-priority

Aspire quick smoke:
  pwsh .\scripts\benchmarks\Run-PublicRepoCoordinatorBenchmark.ps1 -AspireRoot C:\code\aspire -BuildPath Aspire-Core.slnf -NormalBuildCount 2 -HighBuildCount 1 -Rounds 1 -Scenarios coordinator-priority

Defaults on this machine:
  logical processors:       $ProcessorCount
  node budget:              $NodeBudget
  slice:                    $Slice
  reservation:              $Reservation
  priority aging threshold: $PriorityAgingThreshold

Outputs:
  scenario-summary.csv
  <scenario>-r<round>\runs.csv
  <scenario>-r<round>\samples.csv
  per-run stdout/stderr logs
"@
}

function Resolve-FirstExistingPath {
    param([string[]]$Candidates)

    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        $expanded = [Environment]::ExpandEnvironmentVariables($candidate)
        if (Test-Path -LiteralPath $expanded) {
            return (Resolve-Path -LiteralPath $expanded).Path
        }
    }

    return $null
}

function Get-DefaultBuildPath {
    param([string]$Root)

    foreach ($candidate in @('Compilers.slnf', 'Aspire-Core.slnf', 'Aspire.slnx')) {
        if (Test-Path -LiteralPath (Join-Path $Root $candidate)) {
            return $candidate
        }
    }

    $solution = Get-ChildItem -LiteralPath $Root -File -Filter '*.sln*' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $solution) {
        return $solution.Name
    }

    throw "BuildPath was not provided and no solution or solution filter was found under BenchmarkRoot '$Root'."
}

function Get-DefaultTouchFileRelativePath {
    param([string]$Root)

    foreach ($candidate in @(
        'src\Compilers\CSharp\Portable\CSharpCompilationOptions.cs',
        'src\Aspire.Hosting\DistributedApplication.cs'
    )) {
        if (Test-Path -LiteralPath (Join-Path $Root $candidate)) {
            return $candidate
        }
    }

    return ''
}

function Get-SafeName {
    param([string]$Value)

    $builder = [System.Text.StringBuilder]::new($Value.Length)
    foreach ($character in $Value.ToCharArray()) {
        if ([char]::IsLetterOrDigit($character) -or $character -eq '-') {
            [void]$builder.Append([char]::ToLowerInvariant($character))
        }
        else {
            [void]$builder.Append('-')
        }
    }

    return $builder.ToString().Trim('-')
}

function Invoke-Checked {
    param(
        [string]$FileName,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [string]$Label
    )

    Write-Host "[$Label] $FileName $($Arguments -join ' ')"
    Push-Location $WorkingDirectory
    try {
        $output = & $FileName @Arguments 2>&1
        foreach ($line in $output) {
            Write-Host $line
        }

        if ($LASTEXITCODE -ne 0) {
            throw "$Label failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

function New-BuildArguments {
    param(
        [int]$NodeCount,
        [bool]$Restore
    )

    $arguments = [System.Collections.Generic.List[string]]::new()
    if ($UseDotNetBuild -or [string]::IsNullOrWhiteSpace($MSBuildDllPath)) {
        foreach ($argument in @('build', $BuildPath)) {
            [void]$arguments.Add($argument)
        }

        if (-not $Restore) {
            [void]$arguments.Add('--no-restore')
        }
    }
    else {
        foreach ($argument in @($MSBuildDllPath, $BuildPath)) {
            [void]$arguments.Add($argument)
        }

        if ($Restore) {
            [void]$arguments.Add('/restore')
        }
    }

    foreach ($argument in @("/m:$NodeCount", '/v:q', '/nodeReuse:false', '/p:UseSharedCompilation=false')) {
        [void]$arguments.Add($argument)
    }

    return $arguments
}

function Convert-ProcessTimeToSeconds {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return 0.0
    }

    $days = 0
    $time = $Value
    if ($Value.Contains('-')) {
        $parts = $Value.Split('-', 2)
        [void][int]::TryParse($parts[0], [ref]$days)
        $time = $parts[1]
    }

    $segments = $time.Split(':')
    if ($segments.Count -eq 2) {
        $minutes = 0
        $seconds = 0.0
        [void][int]::TryParse($segments[0], [ref]$minutes)
        [void][double]::TryParse($segments[1], [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$seconds)
        return ($days * 86400) + ($minutes * 60) + $seconds
    }

    if ($segments.Count -eq 3) {
        $hours = 0
        $minutes = 0
        $seconds = 0.0
        [void][int]::TryParse($segments[0], [ref]$hours)
        [void][int]::TryParse($segments[1], [ref]$minutes)
        [void][double]::TryParse($segments[2], [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$seconds)
        return ($days * 86400) + ($hours * 3600) + ($minutes * 60) + $seconds
    }

    return 0.0
}

function Get-DescendantProcessIdsWindows {
    param([int[]]$RootProcessIds)

    $processes = Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId
    $childrenByParent = @{}
    foreach ($process in $processes) {
        if (-not $childrenByParent.ContainsKey($process.ParentProcessId)) {
            $childrenByParent[$process.ParentProcessId] = [System.Collections.Generic.List[int]]::new()
        }

        $childrenByParent[$process.ParentProcessId].Add([int]$process.ProcessId)
    }

    $ids = [System.Collections.Generic.HashSet[int]]::new()
    $queue = [System.Collections.Generic.Queue[int]]::new()
    foreach ($rootProcessId in $RootProcessIds) {
        if ($ids.Add($rootProcessId)) {
            $queue.Enqueue($rootProcessId)
        }
    }

    while ($queue.Count -gt 0) {
        $parent = $queue.Dequeue()
        if (-not $childrenByParent.ContainsKey($parent)) {
            continue
        }

        foreach ($child in $childrenByParent[$parent]) {
            if ($ids.Add($child)) {
                $queue.Enqueue($child)
            }
        }
    }

    return [int[]]@($ids)
}

function Get-UnixProcessTable {
    $psPath = if (Test-Path -LiteralPath '/bin/ps') { '/bin/ps' } else { 'ps' }
    $lines = & $psPath -axo pid=,ppid=,rss=,time=,command=
    foreach ($line in $lines) {
        if ($line -match '^\s*(\d+)\s+(\d+)\s+(\d+)\s+(\S+)\s+(.*)$') {
            [pscustomobject]@{
                Id = [int]$Matches[1]
                ParentId = [int]$Matches[2]
                WorkingSetBytes = [int64]$Matches[3] * 1024
                CpuSeconds = Convert-ProcessTimeToSeconds $Matches[4]
                CommandLine = $Matches[5]
            }
        }
    }
}

function Get-TrackedBenchmarkProcesses {
    param([int[]]$RootProcessIds)

    if ($RootProcessIds.Count -eq 0) {
        return @()
    }

    if ($IsWindowsPlatform) {
        $ids = Get-DescendantProcessIdsWindows -RootProcessIds $RootProcessIds
        return @(Get-Process -Id $ids -ErrorAction SilentlyContinue | ForEach-Object {
            [pscustomobject]@{
                Id = $_.Id
                WorkingSetBytes = [int64]$_.WorkingSet64
                CpuSeconds = $_.TotalProcessorTime.TotalSeconds
            }
        })
    }

    $entries = @(Get-UnixProcessTable)
    $childrenByParent = @{}
    foreach ($entry in $entries) {
        if (-not $childrenByParent.ContainsKey($entry.ParentId)) {
            $childrenByParent[$entry.ParentId] = [System.Collections.Generic.List[int]]::new()
        }

        $childrenByParent[$entry.ParentId].Add($entry.Id)
    }

    $ids = [System.Collections.Generic.HashSet[int]]::new()
    $queue = [System.Collections.Generic.Queue[int]]::new()
    foreach ($rootProcessId in $RootProcessIds) {
        if ($ids.Add($rootProcessId)) {
            $queue.Enqueue($rootProcessId)
        }
    }

    while ($queue.Count -gt 0) {
        $parent = $queue.Dequeue()
        if (-not $childrenByParent.ContainsKey($parent)) {
            continue
        }

        foreach ($child in $childrenByParent[$parent]) {
            if ($ids.Add($child)) {
                $queue.Enqueue($child)
            }
        }
    }

    return @($entries | Where-Object { $ids.Contains($_.Id) })
}

function Get-PropertySum {
    param(
        [object[]]$Items,
        [string]$PropertyName
    )

    $sum = 0.0
    foreach ($item in $Items) {
        $sum += [double]$item.$PropertyName
    }

    return $sum
}

function Get-Average {
    param([double[]]$Values)

    if ($Values.Count -eq 0) {
        return 0.0
    }

    $sum = 0.0
    foreach ($value in $Values) {
        $sum += $value
    }

    return $sum / $Values.Count
}

function New-BenchmarkProcess {
    param(
        [string]$Kind,
        [string]$Label,
        [string]$WorkingDirectory,
        [int]$NodeCount,
        [string]$Priority,
        [bool]$UseCoordinator,
        [string]$PipeName,
        [string]$ScenarioDir
    )

    $runDir = Join-Path $ScenarioDir $Label
    New-Item -ItemType Directory -Force -Path $runDir | Out-Null
    $stdoutPath = Join-Path $runDir "$Label.out.log"
    $stderrPath = Join-Path $runDir "$Label.err.log"

    $arguments = New-BuildArguments -NodeCount $NodeCount -Restore $IncludeRestore

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new($DotNetPath)
    foreach ($argument in $arguments) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    foreach ($environmentVariable in $CoordinatorEnvironmentVariables) {
        [void]$startInfo.Environment.Remove($environmentVariable)
    }

    if ($UseCoordinator) {
        $startInfo.Environment['MSBUILDUSECOORDINATOR'] = '1'
        $startInfo.Environment['MSBUILDCOORDINATORPIPENAME'] = $PipeName
        $startInfo.Environment['MSBUILDCOORDINATORNODEBUDGET'] = [string]$NodeBudget
        $startInfo.Environment['MSBUILDCOORDINATORHIGHPRIORITYRESERVEDNODES'] = [string]$Reservation
        $startInfo.Environment['MSBUILDCOORDINATORMAXNODESPERBUILD'] = [string]$Slice
        $startInfo.Environment['MSBUILDCOORDINATORPRIORITYAGINGTHRESHOLD'] = [string]$PriorityAgingThreshold
        $startInfo.Environment['MSBUILDCOORDINATORBUILDREQUESTPRIORITY'] = $Priority
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()

    [pscustomobject]@{
        kind = $Kind
        label = $Label
        priority = $Priority
        nodes = $NodeCount
        process = $process
        stdoutTask = $process.StandardOutput.ReadToEndAsync()
        stderrTask = $process.StandardError.ReadToEndAsync()
        stdout = $stdoutPath
        stderr = $stderrPath
        startTime = Get-Date
        endTime = $null
        exitCode = $null
        completed = $false
    }
}

function Complete-BenchmarkProcess {
    param([object]$Run)

    if ($Run.completed -or -not $Run.process.HasExited) {
        return
    }

    $Run.process.WaitForExit()
    $Run.endTime = Get-Date
    $Run.exitCode = $Run.process.ExitCode
    Set-Content -Path $Run.stdout -Value $Run.stdoutTask.GetAwaiter().GetResult() -Encoding UTF8
    Set-Content -Path $Run.stderr -Value $Run.stderrTask.GetAwaiter().GetResult() -Encoding UTF8
    $Run.completed = $true
}

function Ensure-Worktrees {
    New-Item -ItemType Directory -Force -Path $WorkRoot | Out-Null
    $worktrees = [System.Collections.Generic.List[object]]::new()
    for ($i = 1; $i -le $NormalBuildCount; $i++) {
        $worktrees.Add([pscustomobject]@{ kind = 'normal'; index = $i; path = Join-Path $WorkRoot "normal$i" })
    }

    for ($i = 1; $i -le $HighBuildCount; $i++) {
        $worktrees.Add([pscustomobject]@{ kind = 'high'; index = $i; path = Join-Path $WorkRoot "high$i" })
    }

    foreach ($worktree in $worktrees) {
        if (-not (Test-Path -LiteralPath $worktree.path)) {
            Invoke-Checked -FileName 'git' -Arguments @('-C', $BenchmarkRoot, 'worktree', 'add', '--detach', $worktree.path, 'HEAD') -WorkingDirectory $BenchmarkRoot -Label "worktree-$($worktree.kind)$($worktree.index)"
        }
    }

    return $worktrees.ToArray()
}

function Restore-And-Warm {
    param([object[]]$Worktrees)

    foreach ($worktree in $Worktrees) {
        if ([string]::IsNullOrWhiteSpace($MSBuildDllPath)) {
            Invoke-Checked -FileName $DotNetPath -Arguments @('restore', $BuildPath, '/v:q') -WorkingDirectory $worktree.path -Label "restore-$($worktree.kind)$($worktree.index)"
        }
        else {
            Invoke-Checked -FileName $DotNetPath -Arguments @($MSBuildDllPath, $BuildPath, '/restore', '/v:q', '/nodeReuse:false', '/p:UseSharedCompilation=false') -WorkingDirectory $worktree.path -Label "restore-$($worktree.kind)$($worktree.index)"
        }
    }

    if ($SkipWarm) {
        return
    }

    foreach ($worktree in $Worktrees) {
        Invoke-Checked -FileName $DotNetPath -Arguments (New-BuildArguments -NodeCount $ProcessorCount -Restore $false) -WorkingDirectory $worktree.path -Label "warm-$($worktree.kind)$($worktree.index)"
    }
}

function Touch-BuildInputs {
    param([object[]]$Worktrees)

    if ([string]::IsNullOrWhiteSpace($TouchFileRelativePath)) {
        return
    }

    foreach ($worktree in $Worktrees) {
        $touchPath = Join-Path $worktree.path $TouchFileRelativePath
        if (-not (Test-Path -LiteralPath $touchPath)) {
            throw "Touch file missing: $touchPath"
        }

        (Get-Item -LiteralPath $touchPath).LastWriteTimeUtc = [DateTime]::UtcNow
    }
}

function Invoke-Scenario {
    param(
        [string]$Name,
        [int]$Round,
        [object[]]$Worktrees
    )

    $useCoordinator = $Name -ne 'no-coordinator'
    $highPriority = if ($Name -eq 'coordinator-priority') { 'High' } else { 'Normal' }
    $nodeCount = if ($useCoordinator) { $NodeBudget } else { $ProcessorCount }
    $scenarioLabel = "$Name-r$Round"
    $pipeName = "msbuild-coordinator-$RepositoryName-$PID-$scenarioLabel"
    $scenarioDir = Join-Path $OutputRoot $scenarioLabel
    New-Item -ItemType Directory -Force -Path $scenarioDir | Out-Null

    Write-Host "Starting $scenarioLabel (coordinator=$useCoordinator, high priority=$highPriority, /m:$nodeCount)"
    Invoke-Checked -FileName $DotNetPath -Arguments @('build-server', 'shutdown') -WorkingDirectory $RepoRoot -Label "shutdown-$scenarioLabel"
    Touch-BuildInputs -Worktrees $Worktrees

    $normalWorktrees = @($Worktrees | Where-Object { $_.kind -eq 'normal' } | Sort-Object index)
    $highWorktrees = @($Worktrees | Where-Object { $_.kind -eq 'high' } | Sort-Object index)
    $runs = [System.Collections.Generic.List[object]]::new()
    $samples = [System.Collections.Generic.List[object]]::new()
    $scenarioStartTime = Get-Date
    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $highStarted = $false

    foreach ($worktree in $normalWorktrees) {
        $runs.Add((New-BenchmarkProcess -Kind 'normal' -Label "normal$($worktree.index)" -WorkingDirectory $worktree.path -NodeCount $nodeCount -Priority 'Normal' -UseCoordinator $useCoordinator -PipeName $pipeName -ScenarioDir $scenarioDir))
    }

    while ($true) {
        foreach ($run in @($runs)) {
            Complete-BenchmarkProcess -Run $run
        }

        if (-not $highStarted -and $timer.Elapsed.TotalSeconds -ge $HighDelaySeconds) {
            foreach ($worktree in $highWorktrees) {
                $runs.Add((New-BenchmarkProcess -Kind 'high' -Label "high$($worktree.index)" -WorkingDirectory $worktree.path -NodeCount $nodeCount -Priority $highPriority -UseCoordinator $useCoordinator -PipeName $pipeName -ScenarioDir $scenarioDir))
            }

            $highStarted = $true
        }

        $activeRuns = @($runs | Where-Object { -not $_.completed })
        if ($highStarted -and $activeRuns.Count -eq 0) {
            break
        }

        $rootIds = @($activeRuns | ForEach-Object { $_.process.Id })
        if ($rootIds.Count -gt 0) {
            $processes = @(Get-TrackedBenchmarkProcesses -RootProcessIds $rootIds)
            $workingSetBytes = [int64](Get-PropertySum -Items $processes -PropertyName WorkingSetBytes)
            $cpuSeconds = Get-PropertySum -Items $processes -PropertyName CpuSeconds
            $samples.Add([pscustomobject]@{
                elapsedSec = [Math]::Round($timer.Elapsed.TotalSeconds, 2)
                activeRuns = $activeRuns.Count
                processCount = $processes.Count
                workingSetMB = [Math]::Round($workingSetBytes / 1MB, 1)
                totalCpuSec = [Math]::Round($cpuSeconds, 2)
            })
        }

        Start-Sleep -Milliseconds 500
    }

    $timer.Stop()
    foreach ($run in @($runs)) {
        Complete-BenchmarkProcess -Run $run
    }

    Invoke-Checked -FileName $DotNetPath -Arguments @('build-server', 'shutdown') -WorkingDirectory $RepoRoot -Label "shutdown-after-$scenarioLabel"

    $resultRows = foreach ($run in $runs) {
        $duration = if ($run.endTime -is [DateTime]) { ($run.endTime - $run.startTime).TotalSeconds } else { 0 }
        [pscustomobject]@{
            scenario = $Name
            round = $Round
            label = $run.label
            kind = $run.kind
            priority = $run.priority
            nodes = $run.nodes
            exitCode = $run.exitCode
            startOffsetSec = [Math]::Round(($run.startTime - $scenarioStartTime).TotalSeconds, 2)
            durationSec = [Math]::Round($duration, 2)
            stdout = $run.stdout
            stderr = $run.stderr
        }
    }

    $runsPath = Join-Path $scenarioDir 'runs.csv'
    $samplesPath = Join-Path $scenarioDir 'samples.csv'
    $resultRows | Export-Csv -NoTypeInformation -Path $runsPath
    $samples | Export-Csv -NoTypeInformation -Path $samplesPath

    $peakProcessCount = 0
    $peakWorkingSetMB = 0.0
    foreach ($sample in $samples) {
        if ($sample.processCount -gt $peakProcessCount) { $peakProcessCount = $sample.processCount }
        if ($sample.workingSetMB -gt $peakWorkingSetMB) { $peakWorkingSetMB = $sample.workingSetMB }
    }

    $normalRows = @($resultRows | Where-Object { $_.kind -eq 'normal' })
    $highRows = @($resultRows | Where-Object { $_.kind -eq 'high' })
    $normalDurations = [double[]]@($normalRows | ForEach-Object { [double]$_.durationSec })
    $highDurations = [double[]]@($highRows | ForEach-Object { [double]$_.durationSec })

    [pscustomobject]@{
        scenario = $Name
        round = $Round
        useCoordinator = $useCoordinator
        highPriority = $highPriority
        nodeBudget = $NodeBudget
        slice = $Slice
        reservation = $Reservation
        priorityAgingThreshold = $PriorityAgingThreshold
        normalBuildCount = $NormalBuildCount
        highBuildCount = $HighBuildCount
        totalWallSec = [Math]::Round($timer.Elapsed.TotalSeconds, 2)
        avgNormalDurationSec = [Math]::Round((Get-Average -Values $normalDurations), 2)
        avgHighDurationSec = [Math]::Round((Get-Average -Values $highDurations), 2)
        peakProcessCount = $peakProcessCount
        peakWorkingSetMB = $peakWorkingSetMB
        failedRuns = @($resultRows | Where-Object { $_.exitCode -ne 0 }).Count
        runs = $runsPath
        samples = $samplesPath
    }
}

if ($ShowInstructions) {
    Write-Instructions
    return
}

if ([string]::IsNullOrWhiteSpace($BenchmarkRoot)) {
    $BenchmarkRoot = Resolve-FirstExistingPath @(
        $env:ROSLYN_REPO,
        $env:ASPIRE_REPO,
        (Join-Path (Split-Path -Parent $RepoRoot) 'roslyn'),
        (Join-Path (Split-Path -Parent $RepoRoot) 'aspire'),
        'C:\code\roslyn',
        'C:\code\aspire'
    )
}

if ([string]::IsNullOrWhiteSpace($BenchmarkRoot) -or -not (Test-Path -LiteralPath $BenchmarkRoot)) {
    throw "BenchmarkRoot was not found. Pass -BenchmarkRoot, -RoslynRoot, -AspireRoot, or set ROSLYN_REPO/ASPIRE_REPO."
}

if ([string]::IsNullOrWhiteSpace($RepositoryName)) {
    $RepositoryName = Get-SafeName -Value (Split-Path -Leaf (Resolve-Path -LiteralPath $BenchmarkRoot).Path)
}

if ([string]::IsNullOrWhiteSpace($BuildPath)) {
    $BuildPath = Get-DefaultBuildPath -Root $BenchmarkRoot
}

if (-not (Test-Path -LiteralPath (Join-Path $BenchmarkRoot $BuildPath))) {
    throw "BuildPath '$BuildPath' was not found under BenchmarkRoot '$BenchmarkRoot'."
}

if ([string]::IsNullOrWhiteSpace($TouchFileRelativePath)) {
    $TouchFileRelativePath = Get-DefaultTouchFileRelativePath -Root $BenchmarkRoot
}

if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    $bootstrapDotNet = Join-Path $RepoRoot 'artifacts\bin\bootstrap\core\dotnet.exe'
    $DotNetPath = if (Test-Path -LiteralPath $bootstrapDotNet) { (Resolve-Path -LiteralPath $bootstrapDotNet).Path } else { 'dotnet' }
}

if ([string]::IsNullOrWhiteSpace($MSBuildDllPath)) {
    $bootstrapSdkRoot = Join-Path $RepoRoot 'artifacts\bin\bootstrap\core\sdk'
    if (Test-Path -LiteralPath $bootstrapSdkRoot) {
        $bootstrapSdk = Get-ChildItem -LiteralPath $bootstrapSdkRoot -Directory | Sort-Object Name -Descending | Select-Object -First 1
        if ($null -ne $bootstrapSdk) {
            $bootstrapMSBuildDll = Join-Path $bootstrapSdk.FullName 'MSBuild.dll'
            if (Test-Path -LiteralPath $bootstrapMSBuildDll) {
                $MSBuildDllPath = (Resolve-Path -LiteralPath $bootstrapMSBuildDll).Path
            }
        }
    }
}

if ([string]::IsNullOrWhiteSpace($WorkRoot)) {
    $WorkRoot = Join-Path ([System.IO.Path]::GetTempPath()) "$RepositoryName-coordinator-worktrees-$ProcessorCount"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("$RepositoryName-coordinator-benchmark-" + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

Write-Host "BenchmarkRoot: $BenchmarkRoot"
Write-Host "RepositoryName: $RepositoryName"
Write-Host "BuildPath: $BuildPath"
Write-Host "TouchFileRelativePath: $(if ([string]::IsNullOrWhiteSpace($TouchFileRelativePath)) { '(disabled)' } else { $TouchFileRelativePath })"
Write-Host "DotNetPath: $DotNetPath"
Write-Host "MSBuildDllPath: $(if ([string]::IsNullOrWhiteSpace($MSBuildDllPath)) { '(dotnet build)' } else { $MSBuildDllPath })"
Write-Host "WorkRoot: $WorkRoot"
Write-Host "OutputRoot: $OutputRoot"
Write-Host "Logical processors: $ProcessorCount"
Write-Host "NodeBudget: $NodeBudget"
Write-Host "Slice: $Slice"
Write-Host "Reservation: $Reservation"
Write-Host "PriorityAgingThreshold: $PriorityAgingThreshold"
Write-Host "NormalBuildCount: $NormalBuildCount"
Write-Host "HighBuildCount: $HighBuildCount"
Write-Host "Scenarios: $($Scenarios -join ', ')"

$worktrees = Ensure-Worktrees
if ($Prepare) {
    Restore-And-Warm -Worktrees $worktrees
}

$summaries = [System.Collections.Generic.List[object]]::new()
for ($round = 1; $round -le $Rounds; $round++) {
    foreach ($scenario in $Scenarios) {
        [void]$summaries.Add((Invoke-Scenario -Name $scenario -Round $round -Worktrees $worktrees))
    }
}

$summaryCsv = Join-Path $OutputRoot 'scenario-summary.csv'
$summaries | Export-Csv -NoTypeInformation -Path $summaryCsv
$summaries | Format-Table -AutoSize
Write-Host "Summary CSV: $summaryCsv"

$failed = @($summaries | Where-Object { $_.failedRuns -gt 0 })
if ($failed.Count -gt 0) {
    throw "$($failed.Count) scenario(s) had failed builds. Inspect per-run stderr logs under $OutputRoot."
}
