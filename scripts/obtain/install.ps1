#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

<#
.SYNOPSIS
    Installs dotnet cli
.DESCRIPTION
    Installs dotnet cli. If dotnet installation already exists in the given directory
    it will update it only if the requested version differs from the one already installed.
.PARAMETER Channel
    Default: preview
    Channel is the way of reasoning about stability and quality of dotnet. This parameter takes one of the values:
    - future - Possibly unstable, frequently changing, may contain new finished and unfinished features
    - preview - Pre-release stable with known issues and feature gaps
    - production - Most stable releases
.PARAMETER Version
    Default: latest
    Represents a build version on specific channel. Possible values:
    - 4-part version in a format A.B.C.D - represents specific version of build
    - latest - most latest build on specific channel
    - lkg - last known good version on specific channel
    Note: LKG work is in progress. Once the work is finished, this will become new default
.PARAMETER InstallDir
    Default: %LocalAppData%\Microsoft\.dotnet
    Path to where to install dotnet. Note that binaries will be placed directly in a given directory.
.PARAMETER Architecture
    Default: auto - this value represents currently running OS architecture
    Architecture of dotnet binaries to be installed.
    Possible values are: auto, AMD64 and x86
.PARAMETER DebugSymbols
    If set the installer will include symbols in the installation.
.PARAMETER DryRun
    If set it will not perform installation but instead display what command line to use to consistently install
    currently requested version of dotnet cli. In example if you specify version 'latest' it will display a link
    with specific version so that this command can be used deterministicly in a build script.
    It also displays binaries location if you prefer to install or download it yourself.
.PARAMETER NoPath
    By default this script will set environment variable PATH for the current process to the binaries folder inside installation folder.
    If set it will display binaries location but not set any environment variable.
.PARAMETER Verbose
    Displays diagnostics information.
.PARAMETER AzureFeed
    Default: https://dotnetcli.blob.core.windows.net/dotnet
    This parameter should not be usually changed by user. It allows to change URL for the Azure feed used by this installer.
#>
[cmdletbinding()]
param(
   [string]$Channel="preview",
   [string]$Version="Latest",
   [string]$InstallDir="<usershare>",
   [string]$Architecture="auto",
   [switch]$DebugSymbols, # TODO: Switch does not work yet. Symbols zip is not being uploaded yet.
   [switch]$DryRun,
   [switch]$NoPath,
   [string]$AzureFeed="https://dotnetcli.blob.core.windows.net/dotnet"
)

Set-StrictMode -Version Latest
$ErrorActionPreference="Stop"
$ProgressPreference="SilentlyContinue"

$LocalVersionFileRelativePath="\.version"
$BinFolderRelativePath="\bin"

function Say($str) {
    Write-Host "dotnet_install: $str"
}

function Say-Verbose($str) {
    Write-Verbose "dotnet_install: $str"
}

function Say-Invocation($Invocation) {
    $command = $Invocation.MyCommand;
    $args = (($Invocation.BoundParameters.Keys | foreach { "-$_ `"$($Invocation.BoundParameters[$_])`"" }) -join " ")
    Say-Verbose "$command $args"
}

function Get-Machine-Architecture() {
    Say-Invocation $MyInvocation

    # possible values: AMD64, IA64, x86
    return $ENV:PROCESSOR_ARCHITECTURE
}

# TODO: Architecture and CLIArchitecture should be unified
function Get-CLIArchitecture-From-Architecture([string]$Architecture) {
    Say-Invocation $MyInvocation

    switch ($Architecture.ToLower()) {
        { $_ -eq "auto" } { return Get-CLIArchitecture-From-Architecture $(Get-Machine-Architecture) }
        { ($_ -eq "amd64") -or ($_ -eq "x64") } { return "x64" }
        { $_ -eq "x86" } { return "x86" }
        default { throw "Architecture not supported. If you think this is a bug, please report it at https://github.com/dotnet/cli/issues" }
    }
}

function Get-Version-Info-From-Version-Text([string]$VersionText) {
    Say-Invocation $MyInvocation

    $Data = @($VersionText.Split([char[]]@(), [StringSplitOptions]::RemoveEmptyEntries));

    $VersionInfo = @{}
    $VersionInfo.CommitHash = $Data[0].Trim()
    $VersionInfo.Version = $Data[1].Trim()
    return $VersionInfo
}

function Get-Latest-Version-Info([string]$AzureFeed, [string]$AzureChannel, [string]$CLIArchitecture) {
    Say-Invocation $MyInvocation

    $VersionFileUrl = "$AzureFeed/$AzureChannel/dnvm/latest.win.$CLIArchitecture.version"
    $Response = Invoke-WebRequest -UseBasicParsing $VersionFileUrl
    $VersionText = [Text.Encoding]::UTF8.GetString($Response.Content)

    $VersionInfo = Get-Version-Info-From-Version-Text $VersionText

    return $VersionInfo
}

# TODO: AzureChannel and Channel should be unified
function Get-Azure-Channel-From-Channel([string]$Channel) {
    Say-Invocation $MyInvocation

    # For compatibility with build scripts accept also directly Azure channels names
    switch ($Channel.ToLower()) {
        { ($_ -eq "future") -or ($_ -eq "dev") } { return "dev" }
        { ($_ -eq "preview") -or ($_ -eq "beta") } { return "beta" }
        { $_ -eq "production" } { throw "Production channel does not exist yet" }
        default { throw "``$Channel`` is an invalid channel name. Use one of the following: ``future``, ``preview``, ``production``" }
    }
}

function Get-Specific-Version-From-Version([string]$AzureFeed, [string]$AzureChannel, [string]$CLIArchitecture, [string]$Version) {
    Say-Invocation $MyInvocation

    switch ($Version.ToLower()) {
        { $_ -eq "latest" } {
            $LatestVersionInfo = Get-Latest-Version-Info -AzureFeed $AzureFeed -AzureChannel $AzureChannel -CLIArchitecture $CLIArchitecture
            return $LatestVersionInfo.Version
        }
        { $_ -eq "lkg" } { throw "``-Version LKG`` not supported yet." }
        default { return $Version }
    }
}

function Construct-Download-Link([string]$AzureFeed, [string]$AzureChannel, [string]$SpecificVersion, [string]$CLIArchitecture) {
    Say-Invocation $MyInvocation

    $DownloadLink = "$AzureFeed/$AzureChannel/Binaries/$SpecificVersion/dotnet-win-$CLIArchitecture.$SpecificVersion.zip"
    Say-Verbose "Constructed Download link: $DownloadLink"
    return $DownloadLink
}

function Get-User-Share-Path() {
    Say-Invocation $MyInvocation

    $InstallRoot = $env:DOTNET_INSTALL_DIR
    if (!$InstallRoot) {
        $InstallRoot = "$env:LocalAppData\Microsoft\.dotnet"
    }
    return $InstallRoot
}

function Resolve-Installation-Path([string]$InstallDir) {
    Say-Invocation $MyInvocation

    if ($InstallDir -eq "<usershare>") {
        return Get-User-Share-Path
    }
    return $InstallDir
}

# TODO: check for global.json
function Get-Installed-Version-Info([string]$InstallRoot) {
    Say-Invocation $MyInvocation

    $VersionFile = Join-Path -Path $InstallRoot -ChildPath $LocalVersionFileRelativePath
    Say-Verbose "Local version file: $VersionFile"
    
    if (Test-Path $VersionFile) {
        $VersionText = cat $VersionFile
        Say-Verbose "Local version file text: $VersionText"
        return Get-Version-Info-From-Version-Text $VersionText
    }

    Say-Verbose "Local version file not found."

    return $null
}

function Get-Absolute-Path([string]$RelativeOrAbsolutePath) {
    # Too much spam
    # Say-Invocation $MyInvocation

    return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($RelativeOrAbsolutePath)
}

function Extract-And-Override-Zip([string]$ZipPath, [string]$OutPath) {
    Say-Invocation $MyInvocation

    Add-Type -Assembly System.IO.Compression.FileSystem | Out-Null
    Set-Variable -Name Zip
    try {
        $Zip = [System.IO.Compression.ZipFile]::OpenRead($ZipPath)
        
        foreach ($entry in $Zip.Entries) {
            $DestinationPath = Get-Absolute-Path $(Join-Path -Path $OutPath -ChildPath $entry.FullName)
            $DestinationDir = Split-Path -Parent $DestinationPath
            New-Item -ItemType Directory -Force -Path $DestinationDir | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $DestinationPath, $true)
        }
    }
    finally {
        if ($Zip -ne $null) {
            $Zip.Dispose()
        }
    }
}

$AzureChannel = Get-Azure-Channel-From-Channel -Channel $Channel
$CLIArchitecture = Get-CLIArchitecture-From-Architecture $Architecture
$SpecificVersion = Get-Specific-Version-From-Version -AzureFeed $AzureFeed -AzureChannel $AzureChannel -CLIArchitecture $CLIArchitecture -Version $Version
$DownloadLink = Construct-Download-Link -AzureFeed $AzureFeed -AzureChannel $AzureChannel -SpecificVersion $SpecificVersion -CLIArchitecture $CLIArchitecture

if ($DryRun) {
    Say "Payload URL: $DownloadLink"
    Say "Repeatable invocation: .\$($MyInvocation.MyCommand) -Version $SpecificVersion -Channel $Channel -DebugSymbols `$$DebugSymbols -Architecture $CLIArchitecture -InstallDir $InstallDir -NoPath `$$NoPath"
    return
}

$InstallRoot = Resolve-Installation-Path $InstallDir
Say-Verbose "InstallRoot: $InstallRoot"

$VersionInfo = Get-Installed-Version-Info $InstallRoot
$LocalVersionText = if ($VersionInfo -ne $null) { $VersionInfo.Version } else { "<No version installed>" }
Say-Verbose "Local CLI version is: $LocalVersionText"
if (($VersionInfo -ne $null) -and ($SpecificVersion -eq $VersionInfo.Version)) {
    Say "Your version of CLI is up-to-date."
    return
}

New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null

$ZipPath = [System.IO.Path]::GetTempFileName()

Say "Downloading $DownloadLink"
$resp = Invoke-WebRequest -UseBasicParsing $DownloadLink -OutFile $ZipPath

Say "Extracting zip"
Extract-And-Override-Zip -ZipPath $ZipPath -OutPath $InstallRoot

Say "Removing installation artifacts"
Remove-Item $ZipPath

$BinPath = Get-Absolute-Path $(Join-Path -Path $InstallRoot -ChildPath $BinFolderRelativePath)
if (-Not $NoPath) {
    Say "Adding to current process PATH: `"$BinPath`". Note: This change will not be visible if PowerShell was run as a child process."
    $env:path += ";$BinPath"
}
else {
    Say "Binaries of dotnet can be found in $BinPath"
}

Say "Installation finished"
