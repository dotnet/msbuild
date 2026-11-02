# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $VsixPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop')
{
    Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1')
    Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Management\Microsoft.PowerShell.Management.psd1')
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($VsixPath))

function Read-Entry([string] $Name)
{
    $entry = $zip.GetEntry($Name)
    if (!$entry)
    {
        throw "The active SAC candidate is missing '$Name'. Rebuild it; do not publish a control as the experiment."
    }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { return $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}

try
{
    $setup = Read-Entry 'manifest.json' | ConvertFrom-Json
    $root = 'Contents/MSBuild/Current/Bin/'
    switch ($setup.id)
    {
        'Microsoft.Build' { $contexts = @('MSBuild.exe', 'amd64/MSBuild.exe', 'MSBuild.Coordinator.exe', 'amd64/MSBuild.Coordinator.exe') }
        'Microsoft.Build.Arm64' { $contexts = @('arm64/MSBuild.exe', 'arm64/MSBuild.Coordinator.exe') }
        default { throw "Unsupported MSBuild SAC candidate package '$($setup.id)'." }
    }
    foreach ($entry in $zip.Entries)
    {
        if ($entry.FullName -match '^Contents/MSBuild/Current/Bin/(?:[^/]+/)?(?:Microsoft\.VisualStudio\.Telemetry\.dll|Microsoft\.VisualStudio\.OpenTelemetry\.dll|OpenTelemetry\.Consolidated\.dll|Microsoft\.Extensions\..*\.dll|TelemetryReunification\.json)$')
        {
            throw "The experiment must not ship private telemetry/OTel snapshot ownership: $($entry.FullName)"
        }
    }
    if ($setup.PSObject.Properties['dependencies'] -and
        $setup.dependencies.PSObject.Properties['Microsoft.VisualStudio.MSBuild.TelemetryBinding.Experimental'])
    {
        throw 'The experiment must use the ordinary devenv SAC group, not a custom provider dependency.'
    }
    foreach ($context in $contexts)
    {
        $name = $root + $context + '.config.template'
        $metadata = @($setup.files | Where-Object fileName -eq ('/' + $name))
        if ($metadata.Count -ne 1 -or $metadata[0].useGroupConfig -ne 'devenv' -or !$metadata[0].overwriteBindingRedirects)
        {
            throw "Incomplete ordinary devenv SAC finalizer metadata: $name"
        }
        [xml] $config = Read-Entry $name
        if (@($config.SelectNodes('//*[local-name()="assemblyBinding"]/*[local-name()="dependentAssembly"]') | Where-Object {
            $_.SelectSingleNode('*[local-name()="assemblyIdentity"]').GetAttribute('name') -eq 'Microsoft.VisualStudio.Telemetry'
        }).Count)
        {
            throw "The candidate must let destination SAC supply telemetry policy, not retain a private codeBase: $name"
        }
    }
    Write-Host "Verified ordinary devenv SAC candidate '$($setup.id)': $($contexts.Count) templates, no private telemetry ownership."
    [pscustomobject] @{
        Status = 'standard-SAC-consumer-package-verified'
        Vsix = [IO.Path]::GetFullPath($VsixPath)
        BindingGroup = 'devenv'
        ConfigCount = $contexts.Count
        DestinationRequirement = 'Matching VS standard SAC telemetry/closure package and finalized config; not established by this consumer check.'
    }
}
finally
{
    $zip.Dispose()
}
