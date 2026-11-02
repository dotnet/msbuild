# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [Parameter(Mandatory = $true)][string] $X86Config,
    [Parameter(Mandatory = $true)][string] $X64Config,
    [Parameter(Mandatory = $true)][string] $CoordinatorConfig,
    [Parameter(Mandatory = $true)][string] $CoordinatorX64Config
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop')
{
    Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1')
    Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Management\Microsoft.PowerShell.Management.psd1')
}
$output = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\') + '\'
$null = New-Item -ItemType Directory -Path $output -Force
$contexts = @(
    @{ Name = 'MSBuild.x86'; Source = $X86Config; Prefix = '..\..\..\' },
    @{ Name = 'MSBuild.x64'; Source = $X64Config; Prefix = '..\..\..\..\' },
    @{ Name = 'Coordinator.x86'; Source = $CoordinatorConfig; Prefix = '..\..\..\' },
    @{ Name = 'Coordinator.x64'; Source = $CoordinatorX64Config; Prefix = '..\..\..\..\' }
)
$namespace = 'urn:schemas-microsoft-com:asm.v1'
foreach ($context in $contexts)
{
    [xml] $config = Get-Content -LiteralPath $context.Source -Raw
    $runtime = $config.SelectSingleNode('/configuration/runtime')
    if (!$runtime)
    {
        $runtime = $config.CreateElement('runtime')
        $null = $config.DocumentElement.AppendChild($runtime)
    }
    foreach ($entry in @($runtime.SelectNodes('*[local-name()="assemblyBinding"]/*[local-name()="dependentAssembly"]') | Where-Object {
        $_.SelectSingleNode('*[local-name()="assemblyIdentity"]').GetAttribute('name') -in @('Microsoft.VisualStudio.Telemetry', 'System.ValueTuple')
    }))
    {
        $null = $entry.ParentNode.RemoveChild($entry)
    }
    $bindings = $runtime.SelectSingleNode('*[local-name()="assemblyBinding"]')
    if (!$bindings)
    {
        $bindings = $config.CreateElement('assemblyBinding', $namespace)
        $null = $runtime.AppendChild($bindings)
    }
    $dependency = $config.CreateElement('dependentAssembly', $namespace)
    $dependency.InnerXml = '<assemblyIdentity name="Microsoft.VisualStudio.Telemetry" publicKeyToken="b03f5f7f11d50a3a" culture="neutral" xmlns="' + $namespace + '" /><bindingRedirect oldVersion="0.0.0.0-16.0.0.0" newVersion="16.0.0.0" xmlns="' + $namespace + '" /><codeBase version="16.0.0.0" href="' + $context.Prefix + 'Common7\IDE\PrivateAssemblies\Microsoft.VisualStudio.Telemetry.dll" xmlns="' + $namespace + '" />'
    $null = $bindings.AppendChild($dependency)
    # Match MSBuild/VS's existing facade-to-framework redirect, including the coordinator.
    $valueTuple = $config.CreateElement('dependentAssembly', $namespace)
    $valueTuple.InnerXml = '<assemblyIdentity name="System.ValueTuple" publicKeyToken="cc7b13ffcd2ddd51" culture="neutral" xmlns="' + $namespace + '" /><bindingRedirect oldVersion="0.0.0.0-4.0.5.0" newVersion="4.0.0.0" xmlns="' + $namespace + '" />'
    $null = $bindings.AppendChild($valueTuple)
    $config.Save((Join-Path $output ($context.Name + '.config')))
}
$swr = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'files.swr') -Raw
foreach ($replacement in @(
    @{ Old = '$(X86BinPath)MSBuild.exe.config'; New = 'MSBuild.x86.config' },
    @{ Old = '$(X64BinPath)MSBuild.exe.config'; New = 'MSBuild.x64.config' },
    @{ Old = '$(CoordinatorBinPath)MSBuild.Coordinator.exe.config'; New = 'Coordinator.x86.config' },
    @{ Old = '$(CoordinatorX64BinPath)MSBuild.Coordinator.exe.config'; New = 'Coordinator.x64.config' }
))
{
    $old = 'source=' + $replacement.Old + ' vs.file.useGroupConfig="devenv"'
    if ([regex]::Matches($swr, [regex]::Escape($old)).Count -ne 1)
    {
        throw "The setup config anchor changed: $old"
    }
    $swr = $swr.Replace($old, ('source="' + $output + $replacement.New + '" vs.file.useGroupConfig="msbuild-telemetry-experiment"'))
}
$oldTelemetry = [regex]::Matches($swr, '(?m)^  file source=\$\(X86BinPath\)Microsoft\.VisualStudio\.Telemetry\.dll[^\r\n]*')
if ($oldTelemetry.Count -ne 1)
{
    throw 'The private fallback SDK setup anchor changed.'
}
# The unchanged ARM64 package still probes this file; the unified x86/x64 hosts never bind it.
$swr = $swr.Replace($oldTelemetry[0].Value, '  file source=$(X86BinPath)Microsoft.VisualStudio.Telemetry.dll')
$swr = $swr.Replace('vs.dependencies', "vs.dependencies`r`n  vs.dependency id=Microsoft.VisualStudio.MSBuild.TelemetryBinding.Experimental")
$contract = [ordered] @{
    Experiment = 'Destination VS-owned telemetry SDK; requires the VS IDE companion binding group'
    BindingGroup = 'msbuild-telemetry-experiment'
    ProviderPackage = 'Microsoft.VisualStudio.MSBuild.TelemetryBinding.Experimental'
    Contexts = @($contexts | ForEach-Object { $_.Name })
    Scope = 'VS IDE only. BuildTools, ARM64 and CLR2 task host are not unified by this experiment.'
    Fallback = 'Public SDK retained without unified-host NGEN roots for the unchanged ARM64 consumer only.'
}
[IO.File]::WriteAllText((Join-Path $output 'TelemetryReunification.json'), ($contract | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
$swr = $swr.Replace('  file source=$(X86BinPath)Microsoft.VisualStudio.Telemetry.dll', ('  file source=$(X86BinPath)Microsoft.VisualStudio.Telemetry.dll' + "`r`n" + '  file TelemetryReunification.json source="' + $output + 'TelemetryReunification.json"'))
[IO.File]::WriteAllText((Join-Path $output 'files.swr'), $swr, [Text.UTF8Encoding]::new($false))
Write-Output 'Generated destination-owned telemetry consumer: one VS-owned group for four templates; no producer VS SDK needed.'
