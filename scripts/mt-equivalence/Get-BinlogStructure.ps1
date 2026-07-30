<#
.SYNOPSIS
  Writes the target/task/project execution counts of a binary log to a JSON file.

.DESCRIPTION
  Reads the binlog's event stream with Microsoft.Build from a given SDK directory.

  This deliberately runs as its own process, one candidate SDK directory per launch. Loading MSBuild
  commits the process's default AssemblyLoadContext to one Microsoft.Build identity, so a second
  candidate can only ever fail with "Assembly with same name is already loaded" - which masks the
  real reason the first candidate failed. One process per attempt keeps every attempt hermetic and
  every error report truthful. It also keeps MSBuild's assemblies out of the calling session.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $Binlog,
    [Parameter(Mandatory = $true)][string] $OutFile,
    [Parameter(Mandatory = $true)][string] $ReaderDirectory
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$framework = Join-Path $ReaderDirectory 'Microsoft.Build.Framework.dll'
$engine = Join-Path $ReaderDirectory 'Microsoft.Build.dll'

# Each step is reported separately: a failure here disables a check that the text comparison relies
# on, and the caller can only act on it if it says which step broke and why.
try {
    # LoadFrom also registers the assembly's directory as a probing path, so Microsoft.Build's own
    # dependencies resolve without any custom AssemblyLoadContext plumbing.
    [void][System.Reflection.Assembly]::LoadFrom($framework)
    [void][System.Reflection.Assembly]::LoadFrom($engine)
}
catch {
    throw "loading MSBuild from '$ReaderDirectory' failed: $($_.Exception.Message)"
}

try {
    # -ReferencedAssemblies replaces Add-Type's default reference set, so the framework assemblies
    # the source actually uses have to be named explicitly - without System.Collections the
    # Dictionary<,> in BinlogStructure.cs does not resolve.
    Add-Type -TypeDefinition (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'BinlogStructure.cs')) `
        -ReferencedAssemblies @($engine, $framework, 'System.Collections', 'System.Runtime') -ErrorAction Stop
}
catch {
    throw "compiling BinlogStructure.cs against '$ReaderDirectory' failed (PowerShell $($PSVersionTable.PSVersion)): $($_.Exception.Message)"
}

try {
    $counts = [BinlogStructure]::Collect((Resolve-Path -LiteralPath $Binlog).Path)
}
catch {
    throw "reading '$Binlog' with MSBuild from '$ReaderDirectory' failed: $($_.Exception.Message)"
}

[pscustomobject]@{
    ReaderDirectory  = $ReaderDirectory
    Targets          = $counts.Targets
    TargetsByProject = $counts.TargetsByProject
    Tasks            = $counts.Tasks
    Projects         = $counts.Projects
    Diagnostics      = $counts.Diagnostics
} | ConvertTo-Json -Depth 4 -Compress | Set-Content -LiteralPath $OutFile -Encoding utf8
