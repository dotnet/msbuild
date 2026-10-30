# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $VsInstallPath,
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [Parameter(Mandatory = $true)][string] $X86Config,
    [Parameter(Mandatory = $true)][string] $X64Config,
    [Parameter(Mandatory = $true)][string] $CoordinatorConfig,
    [Parameter(Mandatory = $true)][string] $CoordinatorX64Config,
    [string] $ExpectedSnapshotHash
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
$sourceConfigHash = (Get-FileHash -LiteralPath $devenvConfig -Algorithm SHA256).Hash
$sourceSacHash = (Get-FileHash -LiteralPath $sacConfig -Algorithm SHA256).Hash
[xml] $hostConfig = Get-Content -LiteralPath $devenvConfig -Raw
$hostBindings = @($hostConfig.SelectNodes('//*[local-name()="assemblyBinding"]/*[local-name()="dependentAssembly"]'))
$sac = @(Import-Csv -LiteralPath $sacConfig)
$namespace = 'urn:schemas-microsoft-com:asm.v1'
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

Add-Assembly (Join-Path $ide 'PrivateAssemblies\Microsoft.VisualStudio.Telemetry.dll')
if (!@($assemblies.Values | Where-Object Name -eq 'Microsoft.VisualStudio.OpenTelemetry').Count -or
    !@($assemblies.Values | Where-Object Name -eq 'OpenTelemetry.Consolidated').Count)
{
    throw 'The selected VS SDK is not the OpenTelemetry-era stack this experiment investigates.'
}
$orderedAssemblies = @($assemblies.Values | Sort-Object { $_.Name })
$payload = @($orderedAssemblies | Where-Object Delivery -eq 'payload')
$null = New-Item -ItemType Directory -Path $output -Force
foreach ($assembly in $payload)
{
    $source = Join-Path $vs $assembly.Source
    $destination = Join-Path $output ($assembly.Name + '.dll')
    [IO.File]::Copy($source, $destination, $true)
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $assembly.Hash)
    {
        throw "VS payload changed while being snapshotted: $source"
    }
}

$contexts = @(
    @{ Name = 'MSBuild.x86'; Source = $X86Config; Prefix = '.\'; SacPrefix = '..\..\..\' },
    @{ Name = 'MSBuild.x64'; Source = $X64Config; Prefix = '..\'; SacPrefix = '..\..\..\..\' },
    @{ Name = 'Coordinator.x86'; Source = $CoordinatorConfig; Prefix = '.\'; SacPrefix = '..\..\..\' },
    @{ Name = 'Coordinator.x64'; Source = $CoordinatorX64Config; Prefix = '..\'; SacPrefix = '..\..\..\..\' }
)
foreach ($context in $contexts)
{
    [xml] $config = Get-Content -LiteralPath $context.Source -Raw
    $runtime = $config.SelectSingleNode('/configuration/runtime')
    if (!$runtime)
    {
        $runtime = $config.CreateElement('runtime')
        $null = $config.DocumentElement.AppendChild($runtime)
    }
    $bindings = $runtime.SelectSingleNode('*[local-name()="assemblyBinding"]')
    if (!$bindings)
    {
        $bindings = $config.CreateElement('assemblyBinding', $namespace)
        $null = $runtime.AppendChild($bindings)
    }
    foreach ($assembly in $orderedAssemblies)
    {
        $existing = @($runtime.SelectNodes('*[local-name()="assemblyBinding"]/*[local-name()="dependentAssembly"]') | Where-Object {
            $identity = $_.SelectSingleNode('*[local-name()="assemblyIdentity"]')
            $identity.GetAttribute('name') -eq $assembly.Name -and
            $identity.GetAttribute('publicKeyToken') -eq $assembly.PublicKeyToken
        })
        foreach ($node in $existing)
        {
            $null = $node.ParentNode.RemoveChild($node)
        }
        $dependency = $config.CreateElement('dependentAssembly', $namespace)
        $identity = $config.CreateElement('assemblyIdentity', $namespace)
        $identity.SetAttribute('name', $assembly.Name)
        $identity.SetAttribute('publicKeyToken', $assembly.PublicKeyToken)
        $identity.SetAttribute('culture', $assembly.Culture)
        $null = $dependency.AppendChild($identity)
        $redirect = $config.CreateElement('bindingRedirect', $namespace)
        $redirect.SetAttribute('oldVersion', "0.0.0.0-$($assembly.Version)")
        $redirect.SetAttribute('newVersion', $assembly.Version)
        $null = $dependency.AppendChild($redirect)
        $codeBase = $config.CreateElement('codeBase', $namespace)
        $codeBase.SetAttribute('version', $assembly.Version)
        $href = $context.Prefix + $assembly.Name + '.dll'
        if ($assembly.Delivery -eq 'devenv-sac')
        {
            $href = $context.SacPrefix + $assembly.Source
        }
        $codeBase.SetAttribute('href', $href)
        $null = $dependency.AppendChild($codeBase)
        $null = $bindings.AppendChild($dependency)
    }
    foreach ($assembly in ($frameworkRedirects.Values | Sort-Object { $_.Name }))
    {
        $existing = @($runtime.SelectNodes('*[local-name()="assemblyBinding"]/*[local-name()="dependentAssembly"]') | Where-Object {
            $identity = $_.SelectSingleNode('*[local-name()="assemblyIdentity"]')
            $identity.GetAttribute('name') -eq $assembly.Name -and
            $identity.GetAttribute('publicKeyToken') -eq $assembly.PublicKeyToken
        })
        foreach ($node in $existing)
        {
            $null = $node.ParentNode.RemoveChild($node)
        }
        $dependency = $config.CreateElement('dependentAssembly', $namespace)
        $identity = $config.CreateElement('assemblyIdentity', $namespace)
        $identity.SetAttribute('name', $assembly.Name)
        $identity.SetAttribute('publicKeyToken', $assembly.PublicKeyToken)
        $identity.SetAttribute('culture', 'neutral')
        $null = $dependency.AppendChild($identity)
        $null = $dependency.AppendChild($config.ImportNode($assembly.Redirect, $true))
        $null = $bindings.AppendChild($dependency)
    }
    $config.Save((Join-Path $output ($context.Name + '.config')))
}

$swr = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'files.swr') -Raw
$replacements = @{
    'source=$(X86BinPath)MSBuild.exe.config ' = 'source="' + $output + 'MSBuild.x86.config" '
    'source=$(X64BinPath)MSBuild.exe.config ' = 'source="' + $output + 'MSBuild.x64.config" '
    'source=$(CoordinatorBinPath)MSBuild.Coordinator.exe.config ' = 'source="' + $output + 'Coordinator.x86.config" '
    'source=$(CoordinatorX64BinPath)MSBuild.Coordinator.exe.config ' = 'source="' + $output + 'Coordinator.x64.config" '
}
foreach ($old in $replacements.Keys)
{
    if ([regex]::Matches($swr, [regex]::Escape($old)).Count -ne 1)
    {
        throw "The SWR config anchor changed: $old"
    }
    $swr = $swr.Replace($old, $replacements[$old])
}
$telemetryLine = '  file source=$(X86BinPath)Microsoft.VisualStudio.Telemetry.dll'
$matches = [regex]::Matches($swr, '(?m)^' + [regex]::Escape($telemetryLine) + '[^\r\n]*')
if ($matches.Count -ne 1)
{
    throw 'The SWR telemetry payload anchor changed.'
}
$applications = @(
    'MSBuild.exe', 'amd64\MSBuild.exe',
    'MSBuild.Coordinator.exe', 'amd64\MSBuild.Coordinator.exe'
) | ForEach-Object { 'vs.file.ngenApplications="[installDir]\MSBuild\Current\Bin\' + $_ + '"' }
$metadata = ($applications -join ' ') + ' vs.file.ngenArchitecture=all vs.file.ngenPriority=2'
$lines = @($payload | ForEach-Object {
    '  file source="' + $output + $_.Name + '.dll" ' + $metadata
})
$lines += '  file TelemetryReunification.json source="' + $output + 'TelemetryReunification.json"'
$swr = $swr.Replace($matches[0].Value, ($lines -join "`r`n"))

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
    Experiment = 'VS telemetry SDK reunification; byte-identical snapshot, not physical-file deduplication'
    VsVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $ide 'devenv.exe')).FileVersion
    DevenvConfigHash = $sourceConfigHash
    SacConfigHash = $sourceSacHash
    Assemblies = $orderedAssemblies
    FrameworkReferences = @($framework.Values | Sort-Object { $_.Requested })
    Contexts = @($contexts | ForEach-Object { $_.Name })
}
$manifestPath = Join-Path $output 'TelemetryReunification.json'
[IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
$snapshotHash = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash
if ($ExpectedSnapshotHash -and $snapshotHash -ne $ExpectedSnapshotHash)
{
    throw "VS snapshot mismatch. Expected $ExpectedSnapshotHash; generated $snapshotHash. Do not substitute the build agent's SDK for the intended test host."
}
$swr | Set-Content -LiteralPath (Join-Path $output 'files.swr') -Encoding UTF8
Write-Output "Experimental VS telemetry snapshot: $($payload.Count) payload assemblies; $($orderedAssemblies.Count - $payload.Count) existing devenv SAC assemblies; four executable configs."
Write-Output "VsTelemetryExperimentExpectedSnapshotHash=$snapshotHash"
