<#
.SYNOPSIS
  Writes the target/task/project execution counts of a binary log to a JSON file.

.DESCRIPTION
  Compiles BinlogStructure.cs with the SDK's own Roslyn and runs it on the SDK's own runtime, so the
  program, the compiler and Microsoft.Build all agree on the target framework.

  This deliberately avoids loading Microsoft.Build into the calling PowerShell. The build agents run
  an older pwsh than a typical dev box (7.4, on .NET 8) while the SDK's Microsoft.Build targets a
  newer framework, so Add-Type there fails with CS1705 - it compiles locally and not on the agent.
  It also avoids reading the counts out of a diagnostic-verbosity text replay: engine-assigned
  TargetIds are not unique across projects, and a measurable share of TaskStarted events never reach
  the text at all, so the text cannot give exact counts.

  The compile is done once per work directory and reused.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Binlog,
    [Parameter(Mandatory = $true)][string] $OutFile,
    [Parameter(Mandatory = $true)][string] $WorkDir
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

function Find-DotnetRoot {
    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    $onPath = Get-Command dotnet -ErrorAction SilentlyContinue
    foreach ($root in @(
            (Join-Path $repoRoot '.dotnet'),
            $env:DOTNET_INSTALL_DIR,
            $env:DOTNET_ROOT,
            $(if ($onPath) { Split-Path -Parent $onPath.Source }))) {

        if (-not $root) { continue }
        foreach ($name in 'dotnet.exe', 'dotnet') {
            $exe = Join-Path $root $name
            if (Test-Path -LiteralPath $exe) { return [pscustomobject]@{ Root = $root; Exe = $exe } }
        }
    }
    throw 'No dotnet installation found (looked in <repo>\.dotnet, DOTNET_INSTALL_DIR, DOTNET_ROOT and PATH).'
}

$dotnet = Find-DotnetRoot

# Newest SDK that actually carries Microsoft.Build; that is the assembly set the program binds to.
$sdk = Get-ChildItem -LiteralPath (Join-Path $dotnet.Root 'sdk') -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending |
    Where-Object { (Test-Path -LiteralPath (Join-Path $_.FullName 'Microsoft.Build.dll')) -and (Test-Path -LiteralPath (Join-Path $_.FullName 'Roslyn\bincore\csc.dll')) } |
    Select-Object -First 1
if (-not $sdk) { throw "No SDK under $(Join-Path $dotnet.Root 'sdk') contains both Microsoft.Build.dll and Roslyn." }

# Run on the newest shared framework available: it has to be at least what Microsoft.Build targets.
$framework = Get-ChildItem -LiteralPath (Join-Path $dotnet.Root 'shared\Microsoft.NETCore.App') -Directory -ErrorAction SilentlyContinue |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $framework) { throw "No Microsoft.NETCore.App under $(Join-Path $dotnet.Root 'shared')." }

$toolDir = Join-Path $WorkDir 'binlogstructure'
$toolDll = Join-Path $toolDir 'BinlogStructure.dll'
$source = Join-Path $PSScriptRoot 'BinlogStructure.cs'

if ((-not (Test-Path -LiteralPath $toolDll)) -or ((Get-Item $toolDll).LastWriteTimeUtc -lt (Get-Item $source).LastWriteTimeUtc)) {
    New-Item -ItemType Directory -Force -Path $toolDir | Out-Null

    # Reference the runtime's own assemblies rather than a targeting pack, which need not be
    # installed. Only the managed ones can be referenced, hence the name filter.
    $refs = New-Object System.Collections.Generic.List[string]
    $refs.Add((Join-Path $sdk.FullName 'Microsoft.Build.dll'))
    $refs.Add((Join-Path $sdk.FullName 'Microsoft.Build.Framework.dll'))
    foreach ($dll in (Get-ChildItem -LiteralPath $framework.FullName -Filter '*.dll')) {
        if ($dll.Name -like '*Native*') { continue }
        if ($dll.Name -like 'System*.dll' -or $dll.Name -eq 'netstandard.dll') { $refs.Add($dll.FullName) }
    }

    $cscArgs = @(
        (Join-Path $sdk.FullName 'Roslyn\bincore\csc.dll'),
        '/nologo', '/noconfig', '/nostdlib', '/target:exe', '/optimize+', '/nullable:enable',
        "/out:$toolDll", $source) + ($refs | ForEach-Object { "/r:$_" })

    $cscOutput = & $dotnet.Exe @cscArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "compiling BinlogStructure.cs failed: $(($cscOutput | Out-String).Trim())" }

    # csc alone does not emit one, and 'dotnet exec' will not start a framework-dependent app without it.
    @{
        runtimeOptions = @{
            tfm         = 'net10.0'
            framework   = @{ name = 'Microsoft.NETCore.App'; version = $framework.Name }
            rollForward = 'latestMajor'
        }
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $toolDir 'BinlogStructure.runtimeconfig.json') -Encoding utf8
}

$runOutput = & $dotnet.Exe exec $toolDll $Binlog $OutFile $sdk.FullName 2>&1
if ($LASTEXITCODE -ne 0) { throw "reading '$Binlog' failed: $(($runOutput | Out-String).Trim())" }
