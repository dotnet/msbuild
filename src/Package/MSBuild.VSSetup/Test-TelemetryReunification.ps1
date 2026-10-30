# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $VsixPath,
    [Parameter(Mandatory = $true)][string] $VsInstallPath,
    [Parameter(Mandatory = $true)][string] $ExpectedSnapshotHash
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Desktop')
{
    Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Utility\Microsoft.PowerShell.Utility.psd1')
    Import-Module (Join-Path $PSHOME 'Modules\Microsoft.PowerShell.Management\Microsoft.PowerShell.Management.psd1')
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$vs = [IO.Path]::GetFullPath($VsInstallPath).TrimEnd('\') + '\'
$zip = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($VsixPath))

function Get-Entry([string] $Name)
{
    $entry = $zip.GetEntry($Name)
    if (!$entry)
    {
        throw "Missing VSIX entry: $Name"
    }
    return $entry
}

function Get-EntryText([string] $Name)
{
    $reader = [IO.StreamReader]::new((Get-Entry $Name).Open())
    try
    {
        return $reader.ReadToEnd()
    }
    finally
    {
        $reader.Dispose()
    }
}

function Get-EntryHash([string] $Name)
{
    $stream = (Get-Entry $Name).Open()
    $sha = [Security.Cryptography.SHA256]::Create()
    try
    {
        return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-', '')
    }
    finally
    {
        $sha.Dispose()
        $stream.Dispose()
    }
}

function Get-FileMetadata([string] $Name)
{
    $matches = @($setup.files | Where-Object fileName -eq ('/' + $Name))
    if ($matches.Count -ne 1)
    {
        throw "Missing or duplicate setup file metadata: $Name"
    }
    return $matches[0]
}

try
{
    $root = 'Contents/MSBuild/Current/Bin/'
    $snapshotName = $root + 'TelemetryReunification.json'
    if ((Get-EntryHash $snapshotName) -ne $ExpectedSnapshotHash)
    {
        throw 'The packaged snapshot differs from the explicitly selected producer snapshot.'
    }
    $snapshot = Get-EntryText $snapshotName | ConvertFrom-Json
    $setup = Get-EntryText 'manifest.json' | ConvertFrom-Json
    $devenv = Join-Path $vs 'Common7\IDE\devenv.exe'
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($devenv).FileVersion -ne $snapshot.VsVersion -or
        (Get-FileHash -LiteralPath ($devenv + '.config') -Algorithm SHA256).Hash -ne $snapshot.DevenvConfigHash -or
        (Get-FileHash -LiteralPath (Join-Path $vs 'SharedAssemblies\Configs\devenv.csv') -Algorithm SHA256).Hash -ne $snapshot.SacConfigHash)
    {
        throw 'Destination VS version/config/SAC differs from the snapshot producer. Re-snapshot; do not deploy.'
    }

    $applications = @(
        'MSBuild.exe', 'amd64\MSBuild.exe',
        'MSBuild.Coordinator.exe', 'amd64\MSBuild.Coordinator.exe'
    ) | ForEach-Object { '[installDir]\MSBuild\Current\Bin\' + $_ }
    $payloadBytes = 0L
    foreach ($assembly in $snapshot.Assemblies)
    {
        $source = Join-Path $vs $assembly.Source
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $assembly.Hash)
        {
            throw "Destination SDK dependency differs from the snapshot producer: $source"
        }
        if ($assembly.Delivery -eq 'payload')
        {
            $name = $root + $assembly.Name + '.dll'
            $metadata = Get-FileMetadata $name
            if ((Get-EntryHash $name) -ne $assembly.Hash -or $metadata.sha256 -ne $assembly.Hash)
            {
                throw "Incorrect experimental payload: $name"
            }
            if ($metadata.ngenArchitecture -ne 'All' -or $metadata.ngenPriority -ne 2 -or
                @($metadata.ngenApplications).Count -ne 4 -or
                @(Compare-Object $applications @($metadata.ngenApplications)).Count)
            {
                throw "Incomplete NGEN setup roots: $name"
            }
            $payloadBytes += (Get-Entry $name).Length
        }
        elseif ($assembly.Delivery -ne 'devenv-sac')
        {
            throw "Unknown dependency delivery: $($assembly.Delivery)"
        }
    }

    foreach ($relative in 'MSBuild.exe.config.template', 'amd64/MSBuild.exe.config.template',
        'MSBuild.Coordinator.exe.config.template', 'amd64/MSBuild.Coordinator.exe.config.template')
    {
        $name = $root + $relative
        $metadata = Get-FileMetadata $name
        if ($metadata.useGroupConfig -ne 'devenv' -or !$metadata.overwriteBindingRedirects)
        {
            throw "The existing devenv group-config integration was not preserved: $name"
        }
        [xml] $config = Get-EntryText $name
        $directory = Join-Path $vs 'MSBuild\Current\Bin'
        if ($relative.StartsWith('amd64/'))
        {
            $directory = Join-Path $directory 'amd64'
        }
        foreach ($assembly in $snapshot.Assemblies)
        {
            $bindings = @($config.SelectNodes('//*[local-name()="dependentAssembly"]') | Where-Object {
                $identity = $_.SelectSingleNode('*[local-name()="assemblyIdentity"]')
                $identity.GetAttribute('name') -eq $assembly.Name -and
                $identity.GetAttribute('publicKeyToken') -eq $assembly.PublicKeyToken
            })
            if ($bindings.Count -ne 1)
            {
                throw "Missing or ambiguous $($assembly.Name) binding in $name."
            }
            $redirect = $bindings[0].SelectSingleNode('*[local-name()="bindingRedirect"]')
            $codeBase = $bindings[0].SelectSingleNode('*[local-name()="codeBase"]')
            $expected = Join-Path $vs $assembly.Source
            if ($assembly.Delivery -eq 'payload')
            {
                $expected = Join-Path (Join-Path $vs 'MSBuild\Current\Bin') ($assembly.Name + '.dll')
            }
            if (!$redirect -or !$codeBase -or $redirect.GetAttribute('newVersion') -ne $assembly.Version -or
                $codeBase.GetAttribute('version') -ne $assembly.Version -or
                [IO.Path]::GetFullPath((Join-Path $directory $codeBase.GetAttribute('href'))) -ne $expected)
            {
                throw "Incorrect $($assembly.Name) binding in $name."
            }
        }
    }
    [pscustomobject] @{
        Status = 'package-and-destination-verified'
        Vsix = [IO.Path]::GetFullPath($VsixPath)
        VsVersion = $snapshot.VsVersion
        SnapshotHash = $ExpectedSnapshotHash
        PayloadCount = @($snapshot.Assemblies | Where-Object Delivery -eq 'payload').Count
        PayloadBytes = $payloadBytes
        SharedAssemblyCount = @($snapshot.Assemblies | Where-Object Delivery -eq 'devenv-sac').Count
        ConfigCount = 4
        NativeImageAcceptance = 'Requires separate runtime loader evidence; not established by this package check.'
    }
}
finally
{
    $zip.Dispose()
}
