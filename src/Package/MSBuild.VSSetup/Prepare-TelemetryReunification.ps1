# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#requires -Version 5.1
[CmdletBinding(DefaultParameterSetName = 'X86X64')]
param(
    [Parameter(Mandatory = $true)][string] $OutputDirectory,
    [Parameter(Mandatory = $true, ParameterSetName = 'X86X64')][string] $X86Config,
    [Parameter(Mandatory = $true, ParameterSetName = 'X86X64')][string] $X64Config,
    [Parameter(Mandatory = $true, ParameterSetName = 'X86X64')][string] $CoordinatorConfig,
    [Parameter(Mandatory = $true, ParameterSetName = 'X86X64')][string] $CoordinatorX64Config,
    [Parameter(Mandatory = $true, ParameterSetName = 'Arm64')][string] $Arm64Config,
    [Parameter(Mandatory = $true, ParameterSetName = 'Arm64')][string] $CoordinatorArm64Config
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
if ($PSCmdlet.ParameterSetName -eq 'Arm64')
{
    $swrPath = Join-Path $PSScriptRoot '..\MSBuild.VSSetup.Arm64\files.arm64.swr'
    $contexts = @(
        @{ Name = 'MSBuild.arm64'; Source = $Arm64Config; Anchor = '$(Arm64BinPath)MSBuild.exe.config' },
        @{ Name = 'Coordinator.arm64'; Source = $CoordinatorArm64Config; Anchor = '$(CoordinatorArm64BinPath)MSBuild.Coordinator.exe.config' }
    )
}
else
{
    $swrPath = Join-Path $PSScriptRoot 'files.swr'
    $contexts = @(
        @{ Name = 'MSBuild.x86'; Source = $X86Config; Anchor = '$(X86BinPath)MSBuild.exe.config' },
        @{ Name = 'MSBuild.x64'; Source = $X64Config; Anchor = '$(X64BinPath)MSBuild.exe.config' },
        @{ Name = 'Coordinator.x86'; Source = $CoordinatorConfig; Anchor = '$(CoordinatorBinPath)MSBuild.Coordinator.exe.config' },
        @{ Name = 'Coordinator.x64'; Source = $CoordinatorX64Config; Anchor = '$(CoordinatorX64BinPath)MSBuild.Coordinator.exe.config' }
    )
}
$swr = Get-Content -LiteralPath $swrPath -Raw
foreach ($context in $contexts)
{
    [xml] $config = Get-Content -LiteralPath $context.Source -Raw
    foreach ($entry in @($config.SelectNodes('//*[local-name()="assemblyBinding"]/*[local-name()="dependentAssembly"]') | Where-Object {
        $_.SelectSingleNode('*[local-name()="assemblyIdentity"]').GetAttribute('name') -eq 'Microsoft.VisualStudio.Telemetry'
    }))
    {
        $null = $entry.ParentNode.RemoveChild($entry)
    }
    $config.Save((Join-Path $output ($context.Name + '.config')))
    if ($context.Name -eq 'Coordinator.arm64')
    {
        $old = '  file source=' + $context.Anchor
        $new = '  file MSBuild.Coordinator.exe.config.template source="' + $output + $context.Name + '.config" vs.file.useGroupConfig="devenv" vs.file.overwriteBindingRedirects=yes'
    }
    else
    {
        $old = 'source=' + $context.Anchor + ' vs.file.useGroupConfig="devenv"'
        $new = 'source="' + $output + $context.Name + '.config" vs.file.useGroupConfig="devenv"'
    }
    if ([regex]::Matches($swr, [regex]::Escape($old)).Count -ne 1)
    {
        throw "The ordinary SAC config anchor changed: $old"
    }
    $swr = $swr.Replace($old, $new)
}
if ($PSCmdlet.ParameterSetName -eq 'X86X64')
{
    $telemetry = [regex]::Matches($swr, '(?m)^  file source=\$\(X86BinPath\)Microsoft\.VisualStudio\.Telemetry\.dll[^\r\n]*(?:\r?\n)?')
    if ($telemetry.Count -ne 1)
    {
        throw 'The private telemetry setup payload anchor changed.'
    }
    $swr = $swr.Remove($telemetry[0].Index, $telemetry[0].Length)
}
[IO.File]::WriteAllText((Join-Path $output 'files.swr'), $swr, [Text.UTF8Encoding]::new($false))
Write-Output "Generated ordinary devenv SAC consumer: $($contexts.Count) templates; no private telemetry binding, payload or NGEN ownership."
