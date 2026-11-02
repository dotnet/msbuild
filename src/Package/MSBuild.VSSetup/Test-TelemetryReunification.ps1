# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $VsixPath,
    [string] $VsInstallPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop')
{
    Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1')
    Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Management\Microsoft.PowerShell.Management.psd1')
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($VsixPath))
$group = 'msbuild-telemetry-experiment'
$provider = 'Microsoft.VisualStudio.MSBuild.TelemetryBinding.Experimental'
$root = 'Contents/MSBuild/Current/Bin/'

function Get-EntryText([string] $Name)
{
    $entry = $zip.GetEntry($Name)
    if (!$entry)
    {
        throw "The active destination telemetry candidate lacks required package entry '$Name'. Rebuild it; do not publish a control as the experiment."
    }
    $reader = [IO.StreamReader]::new($entry.Open())
    try { return $reader.ReadToEnd() }
    finally { $reader.Dispose() }
}

try
{
    $contract = Get-EntryText ($root + 'TelemetryReunification.json') | ConvertFrom-Json
    $setup = Get-EntryText 'manifest.json' | ConvertFrom-Json
    if ($contract.BindingGroup -ne $group -or $contract.ProviderPackage -ne $provider -or
        !$setup.dependencies.PSObject.Properties[$provider])
    {
        throw 'The active experiment must require the VS-owned companion provider and select its single combined group. Rebuild with destination routing enabled.'
    }
    $fallback = @($setup.files | Where-Object fileName -eq ('/' + $root + 'Microsoft.VisualStudio.Telemetry.dll'))
    if ($fallback.Count -ne 1 -or $fallback[0].PSObject.Properties['ngenApplications'] -or
        $fallback[0].PSObject.Properties['ngenArchitecture'])
    {
        throw 'The legacy ARM64 fallback must not register unused unified-host telemetry NGEN roots.'
    }
    foreach ($entry in $zip.Entries)
    {
        if ($entry.FullName -match '^Contents/MSBuild/Current/Bin/(Microsoft\.VisualStudio\.OpenTelemetry|OpenTelemetry\.Consolidated|Microsoft\.Extensions\.)')
        {
            throw "The MSBuild candidate must not duplicate destination-owned SDK closure payload '$($entry.FullName)'."
        }
    }
    $contexts = @('MSBuild.exe', 'amd64/MSBuild.exe', 'MSBuild.Coordinator.exe', 'amd64/MSBuild.Coordinator.exe')
    foreach ($context in $contexts)
    {
        $name = $root + $context + '.config.template'
        $metadata = @($setup.files | Where-Object fileName -eq ('/' + $name))
        if ($metadata.Count -ne 1 -or $metadata[0].useGroupConfig -ne $group -or !$metadata[0].overwriteBindingRedirects)
        {
            throw "Incomplete destination group-finalizer metadata: $name"
        }
        [xml] $config = Get-EntryText $name
        $entries = @($config.SelectNodes('//*[local-name()="dependentAssembly"]') | Where-Object {
            $_.SelectSingleNode('*[local-name()="assemblyIdentity"]').GetAttribute('name') -eq 'Microsoft.VisualStudio.Telemetry'
        })
        $prefix = '..\..\..\'
        if ($context.StartsWith('amd64/')) { $prefix = '..\..\..\..\' }
        if ($entries.Count -ne 1 -or
            $entries[0].SelectSingleNode('*[local-name()="codeBase"]').GetAttribute('href') -ne ($prefix + 'Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Telemetry.dll'))
        {
            throw "The template must route telemetry to the destination IDE, not a private MSBuild copy: $name"
        }
        $facades = @($config.SelectNodes('//*[local-name()="dependentAssembly"]') | Where-Object {
            $_.SelectSingleNode('*[local-name()="assemblyIdentity"]').GetAttribute('name') -eq 'System.ValueTuple'
        })
        if ($facades.Count -ne 1 -or $facades[0].SelectSingleNode('*[local-name()="bindingRedirect"]').GetAttribute('newVersion') -ne '4.0.0.0')
        {
            throw "The destination SDK's framework facade redirect is missing: $name"
        }
    }
    $status = 'destination-consumer-package-verified; VS companion integration still required'
    if ($VsInstallPath)
    {
        $vs = [IO.Path]::GetFullPath($VsInstallPath).TrimEnd('\') + '\'
        $configs = Join-Path $vs 'SharedAssemblies\Configs'
        $policyPath = Join-Path $configs 'DestinationTelemetryBinding.json'
        $csv = Join-Path $configs ($group + '.csv')
        foreach ($required in $policyPath, $csv, (Join-Path $vs 'Common7\IDE\devenv.exe.config'))
        {
            if (!(Test-Path -LiteralPath $required -PathType Leaf))
            {
                throw "Destination requirement '$required' is missing. Install the matching VS IDE companion provider and finalize the four configs before running the experiment; no old-policy fallback is supported."
            }
        }
        $policy = Get-Content -LiteralPath $policyPath -Raw | ConvertFrom-Json
        if ($policy.BindingGroup -ne $group -or $policy.ProviderPackage -ne $provider -or
            (Get-FileHash -LiteralPath $csv -Algorithm SHA256).Hash -ne $policy.GroupConfigHash -or
            (Get-FileHash -LiteralPath (Join-Path $vs 'Common7\IDE\devenv.exe.config') -Algorithm SHA256).Hash -ne $policy.DevenvConfigHash -or
            (Get-FileHash -LiteralPath (Join-Path $configs 'devenv.csv') -Algorithm SHA256).Hash -ne $policy.SacConfigHash)
        {
            throw 'The VS-owned provider does not match the destination host/SAC policy. Regenerate it with that VS build before finalizing MSBuild.'
        }
        foreach ($assembly in $policy.Assemblies)
        {
            $path = Join-Path $vs $assembly.Source
            if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $assembly.Hash)
            {
                throw "Destination-owned dependency bytes differ from the provider: $path"
            }
            foreach ($context in $contexts)
            {
                $configPath = Join-Path (Join-Path $vs 'MSBuild\Current\Bin') ($context.Replace('/', '\') + '.config')
                [xml] $final = Get-Content -LiteralPath $configPath -Raw
                $bindings = @($final.SelectNodes('//*[local-name()="dependentAssembly"]') | Where-Object {
                    $_.SelectSingleNode('*[local-name()="assemblyIdentity"]').GetAttribute('name') -eq $assembly.Name -and
                    $_.SelectSingleNode('*[local-name()="assemblyIdentity"]').GetAttribute('publicKeyToken') -eq $assembly.PublicKeyToken
                })
                $codeBases = @($bindings | ForEach-Object { $_.SelectNodes('*[local-name()="codeBase"]') } | Where-Object {
                    $_.GetAttribute('version') -eq $assembly.Version
                })
                $redirects = @($bindings | ForEach-Object { $_.SelectNodes('*[local-name()="bindingRedirect"]') } | Where-Object {
                    $_.GetAttribute('newVersion') -eq $assembly.Version
                })
                if ($codeBases.Count -ne 1 -or $redirects.Count -ne 1 -or
                    [IO.Path]::GetFullPath((Join-Path (Split-Path $configPath) $codeBases[0].GetAttribute('href'))) -ne [IO.Path]::GetFullPath($path))
                {
                    throw "Finalized $context does not bind $($assembly.Name) to the destination-owned dependency. Run the matching single-group VS finalizer before testing."
                }
            }
        }
        $status = 'destination-owner-policy-and-four-finalized-configs-verified'
    }
    Write-Host "Verified telemetry destination route: $group; four templates; no producer SDK dependency or copied OTel closure."
    [pscustomobject] @{
        Status = $status
        Vsix = [IO.Path]::GetFullPath($VsixPath)
        BindingGroup = $group
        ProviderPackage = $provider
        ConfigCount = 4
        RuntimeAndNgenAcceptance = 'Requires actual loader/session evidence; this package check is not a performance result.'
    }
}
finally
{
    $zip.Dispose()
}
