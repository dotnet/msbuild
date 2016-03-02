#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

[cmdletbinding()]
param(
   [string]$Channel="nightly",
   [string]$Version="Latest",
   [string]$InstallDir="<usershare>",
   [string]$Architecture="auto",
   [switch]$DryRun,
   [bool]$DebugSymbols=$false, # TODO: There is no zip uploaded yet
   [bool]$NoPath=$false
)

Set-StrictMode -Version Latest
$ErrorActionPreference="Stop"
$ProgressPreference="SilentlyContinue"

$AzureFeed="https://dotnetcli.blob.core.windows.net/dotnet"
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
        { ($_ -eq "amd64") -Or ($_ -eq "x64") } { return "x64" }
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

    switch ($Channel.ToLower()) {
        { $_ -eq "nightly" } { return "dev" }
        { $_ -eq "preview" } { return "beta" }
        { $_ -eq "production" } { throw "Production channel does not exist yet" }
        default { throw "``$Channel`` is an invalid channel name. Use one of the following: ``nightly``, ``preview``, ``production``" }
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

    $VersionFile = $InstallRoot + $LocalVersionFileRelativePath
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

if (-Not $NoPath) {
    $BinPath = Get-Absolute-Path $(Join-Path -Path $InstallRoot -ChildPath $BinFolderRelativePath)
    Say "Adding to PATH: `"$BinPath`""
    $env:path += ";$BinPath"
}

Say "Installation finished"
