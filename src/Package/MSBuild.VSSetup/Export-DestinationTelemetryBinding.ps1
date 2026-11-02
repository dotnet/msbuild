# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $VsInstallPath,
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [Parameter(Mandatory = $true)][version] $PackageVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop')
{
    throw 'Run this metadata-only snapshot tool with Windows PowerShell (powershell.exe), not pwsh.'
}

# A build launched from pwsh can pass a PSModulePath without the Windows PowerShell modules.
Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1')
Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Management\Microsoft.PowerShell.Management.psd1')

$vs = [IO.Path]::GetFullPath($VsInstallPath).TrimEnd('\') + '\'
$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\') + '\'
if ($output.StartsWith($vs, [StringComparison]::OrdinalIgnoreCase) -or $vs.StartsWith($output, [StringComparison]::OrdinalIgnoreCase))
{
    throw 'The output directory must be separate from the source VS installation.'
}

$ide = Join-Path $vs 'Common7\IDE'
$devenvConfig = Join-Path $ide 'devenv.exe.config'
$sacConfig = Join-Path $vs 'SharedAssemblies\Configs\devenv.csv'
$telemetryPath = Join-Path $ide 'PrivateAssemblies\Microsoft.VisualStudio.Telemetry.dll'
foreach ($required in $devenvConfig, $sacConfig, $telemetryPath, (Join-Path $ide 'devenv.exe'))
{
    if (!(Test-Path -LiteralPath $required -PathType Leaf))
    {
        throw "Destination telemetry binding requires the complete VS IDE layout. '$required' is missing. Generate this companion only from the matching VS IDE build; BuildTools and outside-VS are not supported."
    }
}
$telemetryVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($telemetryPath).FileVersion
$telemetryHash = (Get-FileHash -LiteralPath $telemetryPath -Algorithm SHA256).Hash
if ([version] $telemetryVersion -lt [version] '18.0.0.0')
{
    throw "The VS IDE layout has legacy SDK $telemetryVersion. This companion requires a VS-owned OpenTelemetry-era SDK, not MSBuild's private public-package fallback."
}
Write-Output "Destination-owned telemetry policy: $vs; SDK $telemetryVersion, SHA-256 $telemetryHash."
$sourceConfigHash = (Get-FileHash -LiteralPath $devenvConfig -Algorithm SHA256).Hash
$sourceSacHash = (Get-FileHash -LiteralPath $sacConfig -Algorithm SHA256).Hash
[xml] $hostConfig = Get-Content -LiteralPath $devenvConfig -Raw
$hostBindings = @($hostConfig.SelectNodes('//*[local-name()="assemblyBinding"]/*[local-name()="dependentAssembly"]'))
$sac = @(Import-Csv -LiteralPath $sacConfig)
$assemblies = @{}
$framework = @{}
$frameworkRedirects = @{}
$frameworkListPath = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\RedistList\FrameworkList.xml'
[xml] $frameworkList = Get-Content -LiteralPath $frameworkListPath -Raw

function Get-Token([Reflection.AssemblyName] $Identity)
{
    return ([BitConverter]::ToString($Identity.GetPublicKeyToken())).Replace('-', '').ToLowerInvariant()
}

function Get-Key([Reflection.AssemblyName] $Identity)
{
    return '{0}|{1}|{2}' -f $Identity.Name, (Get-Token $Identity), $Identity.CultureName
}

function Get-HostBinding([Reflection.AssemblyName] $Reference)
{
    $token = Get-Token $Reference
    $culture = $Reference.CultureName
    if (!$culture)
    {
        $culture = 'neutral'
    }

    $matching = @($hostBindings | Where-Object {
        $identity = $_.SelectSingleNode('*[local-name()="assemblyIdentity"]')
        $identity.GetAttribute('name') -eq $Reference.Name -and
        $identity.GetAttribute('publicKeyToken') -eq $token -and
        $identity.GetAttribute('culture') -eq $culture
    })
    $version = $Reference.Version
    $redirects = @($matching | ForEach-Object { $_.SelectNodes('*[local-name()="bindingRedirect"]') } | Where-Object {
        $range = $_.GetAttribute('oldVersion').Split('-')
        $first = [version] $range[0]
        $last = $first
        if ($range.Length -eq 2)
        {
            $last = [version] $range[1]
        }
        $Reference.Version -ge $first -and $Reference.Version -le $last
    })
    if ($redirects.Count -gt 1)
    {
        throw "Ambiguous VS binding redirects for $Reference."
    }
    if ($redirects.Count -eq 1)
    {
        $version = [version] $redirects[0].GetAttribute('newVersion')
    }
    $codeBases = @($matching | ForEach-Object { $_.SelectNodes('*[local-name()="codeBase"]') } | Where-Object {
        [version] $_.GetAttribute('version') -eq $version
    })
    if ($codeBases.Count -gt 1)
    {
        throw "Ambiguous VS codeBases for $Reference."
    }
    $path = $null
    if ($codeBases.Count -eq 1)
    {
        $path = [IO.Path]::GetFullPath((Join-Path $ide $codeBases[0].GetAttribute('href')))
    }
    return @{ Version = $version; Path = $path; Redirect = $(if ($redirects.Count) { $redirects[0] } else { $null }) }
}

function Add-Assembly([string] $Path)
{
    $path = [IO.Path]::GetFullPath($Path)
    if (!$path.StartsWith($vs, [StringComparison]::OrdinalIgnoreCase))
    {
        throw "A VS runtime dependency resolved outside the selected installation: $path"
    }
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($path)
    $identity = $assembly.GetName()
    if ($identity.CultureName)
    {
        throw "Localized runtime assemblies are not supported by this experiment: $identity"
    }
    $key = Get-Key $identity
    if ($assemblies.ContainsKey($key))
    {
        if ($assemblies[$key].Identity -ne $identity.FullName -or $assemblies[$key].Hash -ne (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash)
        {
            throw "Inconsistent dependency versions or bytes for $identity."
        }
        return
    }

    $relative = $path.Substring($vs.Length)
    $shared = $relative.StartsWith('SharedAssemblies\', [StringComparison]::OrdinalIgnoreCase)
    if ($shared)
    {
        $rows = @($sac | Where-Object {
            $_.name -eq $identity.Name -and $_.publickeytoken -eq (Get-Token $identity) -and
            [IO.Path]::GetFullPath((Join-Path (Split-Path $sacConfig) $_.href)) -eq $path
        })
        if ($rows.Count -ne 1)
        {
            throw "The dependency is not uniquely supplied by the existing devenv SAC group: $identity"
        }
    }
    $assemblies[$key] = [ordered] @{
        Name = $identity.Name
        Identity = $identity.FullName
        Version = $identity.Version.ToString()
        PublicKeyToken = Get-Token $identity
        Culture = 'neutral'
        Mvid = $assembly.ManifestModule.ModuleVersionId.ToString()
        FileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($path).FileVersion
        Source = $relative
        Hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        Delivery = $(if ($shared) { 'devenv-sac' } else { 'payload' })
    }
    foreach ($reference in $assembly.GetReferencedAssemblies())
    {
        $binding = Get-HostBinding $reference
        if ($binding.Path)
        {
            $actual = [Reflection.AssemblyName]::GetAssemblyName($binding.Path)
            if ((Get-Key $actual) -ne (Get-Key $reference) -or $actual.Version -ne $binding.Version)
            {
                throw "VS codeBase identity mismatch for $reference at $($binding.Path)."
            }
            Add-Assembly $binding.Path
            continue
        }

        # Reflection-only loading does not implement framework unification (notably System.Net.Http 4.2).
        $contract = @($frameworkList.FileList.File | Where-Object {
            $_.AssemblyName -eq $reference.Name -and $_.PublicKeyToken -eq (Get-Token $reference) -and
            [version] $_.Version -ge $binding.Version
        })
        if ($contract.Count -ne 1)
        {
            throw "No explicit VS binding or .NET Framework 4.7.2 contract for $reference, referenced by $identity."
        }
        if ($binding.Redirect)
        {
            $frameworkRedirects[(Get-Key $reference)] = @{
                Name = $reference.Name
                PublicKeyToken = Get-Token $reference
                Redirect = $binding.Redirect
            }
        }
        $framework[$reference.FullName] = [ordered] @{
            Requested = $reference.FullName
            Framework = '.NETFramework,Version=v4.7.2'
            ContractVersion = $contract[0].Version
        }
    }
}

Add-Assembly $telemetryPath
if (!@($assemblies.Values | Where-Object Name -eq 'Microsoft.VisualStudio.OpenTelemetry').Count -or
    !@($assemblies.Values | Where-Object Name -eq 'OpenTelemetry.Consolidated').Count)
{
    throw 'The selected VS SDK is not the OpenTelemetry-era stack this experiment investigates.'
}
$orderedAssemblies = @($assemblies.Values | Sort-Object { $_.Name })
$null = New-Item -ItemType Directory -Path $output -Force
$rows = [Collections.Generic.List[string]]::new()
$rows.Add('name,publickeytoken,culture,oldVersion,newVersion,version,href')
if ($sac.Count -eq 0 -or ($sac[0].PSObject.Properties.Name -join ',') -ne 'name,publickeytoken,culture,oldVersion,newVersion,version,href')
{
    throw 'The destination SAC CSV schema differs from the verified VS finalizer contract. Do not emit an incomplete provider.'
}
foreach ($row in $sac)
{
    $rows.Add("$($row.name),$($row.publickeytoken),$($row.culture),$($row.oldVersion),$($row.newVersion),$($row.version),$($row.href)")
}
foreach ($assembly in ($orderedAssemblies | Where-Object Delivery -eq 'payload'))
{
    if (@($sac | Where-Object name -eq $assembly.Name).Count)
    {
        throw "Private policy conflicts with existing SAC member $($assembly.Name). Do not mutate the global devenv group."
    }
    $href = '..\..\' + $assembly.Source
    $rows.Add("$($assembly.Name),$($assembly.PublicKeyToken),neutral,0.0.0.0,$($assembly.Version),$($assembly.Version),$href")
    $assembly.Delivery = 'vs-private'
}
$csvPath = Join-Path $output 'msbuild-telemetry-experiment.csv'
[IO.File]::WriteAllLines($csvPath, $rows, [Text.UTF8Encoding]::new($false))

foreach ($assembly in $orderedAssemblies)
{
    if ((Get-FileHash -LiteralPath (Join-Path $vs $assembly.Source) -Algorithm SHA256).Hash -ne $assembly.Hash)
    {
        throw "VS changed during snapshot generation: $($assembly.Source)"
    }
}
if ((Get-FileHash -LiteralPath $devenvConfig -Algorithm SHA256).Hash -ne $sourceConfigHash -or
    (Get-FileHash -LiteralPath $sacConfig -Algorithm SHA256).Hash -ne $sourceSacHash)
{
    throw 'VS configuration changed during snapshot generation.'
}
$manifest = [ordered] @{
    Experiment = 'Destination VS-owned SDK policy; VS companion authoring, not an MSBuild producer snapshot'
    VsVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $ide 'devenv.exe')).FileVersion
    DevenvConfigHash = $sourceConfigHash
    SacConfigHash = $sourceSacHash
    Assemblies = $orderedAssemblies
    FrameworkReferences = @($framework.Values | Sort-Object { $_.Requested })
    BindingGroup = 'msbuild-telemetry-experiment'
    ProviderPackage = 'Microsoft.VisualStudio.MSBuild.TelemetryBinding.Experimental'
    GroupConfigHash = (Get-FileHash -LiteralPath $csvPath -Algorithm SHA256).Hash
}
$manifestPath = Join-Path $output 'DestinationTelemetryBinding.json'
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
$snapshotHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
$swr = @"
use vs

package name=Microsoft.VisualStudio.MSBuild.TelemetryBinding.Experimental
        version=$PackageVersion
        vs.package.productArch=neutral

folder "InstallDir:\SharedAssemblies\Configs"
  file source="$csvPath" vs.file.groupConfig="msbuild-telemetry-experiment"
  file source="$manifestPath"
"@
[IO.File]::WriteAllText((Join-Path $output 'companion.swr'), $swr, [Text.UTF8Encoding]::new($false))
$requests = [ordered] @{
    Status = 'Required VS-owner authoring; no registration performed by this exporter'
    Applications = @('MSBuild.exe', 'amd64\MSBuild.exe', 'MSBuild.Coordinator.exe', 'amd64\MSBuild.Coordinator.exe') |
        ForEach-Object { '[installDir]\MSBuild\Current\Bin\' + $_ }
    Architecture = 'all'
    Priority = 2
    Assemblies = $orderedAssemblies
    Requirement = 'Preserve existing owner roots and add these contexts only in the VS-owned private/SAC file packages. Never claim or duplicate those DLLs in this companion or MSBuild.'
}
[IO.File]::WriteAllText((Join-Path $output 'NgenOwnerRequests.json'), ($requests | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output "VS-owned companion policy: $($orderedAssemblies.Count) closure assemblies plus unchanged devenv SAC rows. No SDK DLLs copied. Manifest SHA-256: $snapshotHash"
