#Requires -Version 2

if (Test-Path env:WEBSITE_SITE_NAME)
{
    # This script is run in Azure Web Sites
    # Disable progress indicator
    $ProgressPreference = "SilentlyContinue"
}

$ScriptPath = $MyInvocation.MyCommand.Definition

$Script:UseWriteHost = $true
function _WriteDebug($msg) {
    if($Script:UseWriteHost) {
        try {
            Write-Debug $msg
        } catch {
            $Script:UseWriteHost = $false
            _WriteDebug $msg
        }
    }
}

function _WriteOut {
    param(
        [Parameter(Mandatory=$false, Position=0, ValueFromPipeline=$true)][string]$msg,
        [Parameter(Mandatory=$false)][ConsoleColor]$ForegroundColor,
        [Parameter(Mandatory=$false)][ConsoleColor]$BackgroundColor,
        [Parameter(Mandatory=$false)][switch]$NoNewLine)

    if($__TestWriteTo) {
        $cur = Get-Variable -Name $__TestWriteTo -ValueOnly -Scope Global -ErrorAction SilentlyContinue
        $val = $cur + "$msg"
        if(!$NoNewLine) {
            $val += [Environment]::NewLine
        }
        Set-Variable -Name $__TestWriteTo -Value $val -Scope Global -Force
        return
    }

    if(!$Script:UseWriteHost) {
        if(!$msg) {
            $msg = ""
        }
        if($NoNewLine) {
            [Console]::Write($msg)
        } else {
            [Console]::WriteLine($msg)
        }
    }
    else {
        try {
            if(!$ForegroundColor) {
                $ForegroundColor = $host.UI.RawUI.ForegroundColor
            }
            if(!$BackgroundColor) {
                $BackgroundColor = $host.UI.RawUI.BackgroundColor
            }

            Write-Host $msg -ForegroundColor:$ForegroundColor -BackgroundColor:$BackgroundColor -NoNewLine:$NoNewLine
        } catch {
            $Script:UseWriteHost = $false
            _WriteOut $msg
        }
    }
}

### Constants
$ProductVersion="1.0.0"
$BuildVersion="{{BUILD_VERSION}}"
$Authors="{{AUTHORS}}"

# If the Version hasn't been replaced...
# We can't compare directly with the build version token
# because it'll just get replaced here as well :)
if($BuildVersion.StartsWith("{{")) {
    # We're being run from source code rather than the "compiled" artifact
    $BuildVersion = "HEAD"
}
$FullVersion="$ProductVersion-$BuildVersion"

Set-Variable -Option Constant "CommandName" ([IO.Path]::GetFileNameWithoutExtension($ScriptPath))
Set-Variable -Option Constant "CommandFriendlyName" ".NET Version Manager"
Set-Variable -Option Constant "DefaultUserDirectoryName" ".dotnet"
Set-Variable -Option Constant "DefaultGlobalDirectoryName" "dotnet"
Set-Variable -Option Constant "OldUserDirectoryNames" @(".kre", ".k")
Set-Variable -Option Constant "RuntimePackageName" "dotnet"
Set-Variable -Option Constant "DefaultFeed" "https://distaspnet.blob.core.windows.net/dotnet"
Set-Variable -Option Constant "DefaultFeedKey" "DNX_FEED"
Set-Variable -Option Constant "DefaultUnstableFeed" "https://aspdist.blob.core.windows.net/assets/dnvm/"
Set-Variable -Option Constant "DefaultUnstableFeedKey" "DNX_UNSTABLE_FEED"
Set-Variable -Option Constant "CrossGenCommand" "dnx-crossgen"
Set-Variable -Option Constant "OldCrossGenCommand" "k-crossgen"
Set-Variable -Option Constant "CommandPrefix" "dnvm-"
Set-Variable -Option Constant "DefaultArchitecture" "x64"
Set-Variable -Option Constant "DefaultRuntime" "coreclr"
Set-Variable -Option Constant "AliasExtension" ".txt"
Set-Variable -Option Constant "DefaultOperatingSystem" "win"
Set-Variable -Option Constant "InstallSubfolder" "sdks"

# These are intentionally using "%" syntax. The environment variables are expanded whenever the value is used.
Set-Variable -Option Constant "OldUserHomes" @("%USERPROFILE%\.kre", "%USERPROFILE%\.k")
Set-Variable -Option Constant "DefaultUserHome" "%USERPROFILE%\$DefaultUserDirectoryName"
Set-Variable -Option Constant "HomeEnvVar" "DOTNET_HOME"

Set-Variable -Option Constant "RuntimeShortFriendlyName" "Dotnet"

Set-Variable -Option Constant "DNVMUpgradeUrl" "https://raw.githubusercontent.com/aspnet/Home/dev/dnvm.ps1"

Set-Variable -Option Constant "AsciiArt" @"
   ___  _  ___   ____  ___
  / _ \/ |/ / | / /  |/  /
 / // /    /| |/ / /|_/ / 
/____/_/|_/ |___/_/  /_/  
"@

$ExitCodes = @{
    "Success"                   = 0
    "AliasDoesNotExist"         = 1001
    "UnknownCommand"            = 1002
    "InvalidArguments"          = 1003
    "OtherError"                = 1004
    "NoSuchPackage"             = 1005
    "NoRuntimesOnFeed"          = 1006
}

$ColorScheme = $DnvmColors
if(!$ColorScheme) {
    $ColorScheme = @{
        "Banner"=[ConsoleColor]::Cyan
        "RuntimeName"=[ConsoleColor]::Yellow
        "Help_Header"=[ConsoleColor]::Yellow
        "Help_Switch"=[ConsoleColor]::Green
        "Help_Argument"=[ConsoleColor]::Cyan
        "Help_Optional"=[ConsoleColor]::Gray
        "Help_Command"=[ConsoleColor]::DarkYellow
        "Help_Executable"=[ConsoleColor]::DarkYellow
        "Feed_Name"=[ConsoleColor]::Cyan
        "Warning" = [ConsoleColor]::Yellow
        "Error" = [ConsoleColor]::Red
        "ActiveRuntime" = [ConsoleColor]::Cyan
    }
}

Set-Variable -Option Constant "OptionPadding" 20
Set-Variable -Option Constant "CommandPadding" 15

# Test Control Variables
if($__TeeTo) {
    _WriteDebug "Saving output to '$__TeeTo' variable"
    Set-Variable -Name $__TeeTo -Value "" -Scope Global -Force
}

# Commands that have been deprecated but do still work.
$DeprecatedCommands = @("unalias")

# Load Environment variables
$RuntimeHomes = $env:DOTNET_HOME
$UserHome = $env:DOTNET_USER_HOME
$GlobalHome = $env:DOTNET_GLOBAL_HOME
$ActiveFeed = $env:DNX_FEED
$ActiveUnstableFeed = $env:DNX_UNSTABLE_FEED

# Default Exit Code
$Script:ExitCode = $ExitCodes.Success

############################################################
### Below this point, the terms "DNVM", "DNX", etc.      ###
### should never be used. Instead, use the Constants     ###
### defined above                                        ###
############################################################
# An exception to the above: The commands are defined by functions
# named "dnvm-[command name]" so that extension functions can be added

$StartPath = $env:PATH

if($CmdPathFile) {
    if(Test-Path $CmdPathFile) {
        _WriteDebug "Cleaning old CMD PATH file: $CmdPathFile"
        Remove-Item $CmdPathFile -Force
    }
    _WriteDebug "Using CMD PATH file: $CmdPathFile"
}

# Determine where SDKs can exist (RuntimeHomes)
if(!$RuntimeHomes) {
    # Set up a default value for the runtime home
    $UnencodedHomes = "$env:USERPROFILE\$DefaultUserDirectoryName;$GlobalHome\$DefaultGlobalDirectoryName"
} else {
    $UnencodedHomes = $RuntimeHomes
}

# Determine the default global installation directory (GlobalHome)
if(!$GlobalHome) {
    if($env:ProgramData) {
        $GlobalHome = "$env:ProgramData\$DefaultGlobalDirectoryName"
    } else {
        $GlobalHome = "$env:AllUsersProfile\$DefaultGlobalDirectoryName"
    }

    $env:DOTNET_GLOBAL_HOME="$GlobalHome"
    $env:DOTNET_HOME="$env:DOTNET_HOME;$env:DOTNET_GLOBAL_HOME"
    $UnencodedHomes = "$UnencodedHomes;$GlobalHome"
}

$UnencodedHomes = $UnencodedHomes.Split(";")
$RuntimeHomes = $UnencodedHomes | ForEach-Object { [Environment]::ExpandEnvironmentVariables($_) }
$RuntimeDirs = $RuntimeHomes | ForEach-Object { Join-Path $_ $InstallSubFolder }

# Determine the default installation directory (UserHome)
if(!$UserHome) {
    _WriteDebug "Detecting User Home..."
    $pf = $env:ProgramFiles
    if(Test-Path "env:\ProgramFiles(x86)") {
        $pf32 = Get-Content "env:\ProgramFiles(x86)"
    }

    # Canonicalize so we can do StartsWith tests
    if(!$pf.EndsWith("\")) { $pf += "\" }
    if($pf32 -and !$pf32.EndsWith("\")) { $pf32 += "\" }

    $UserHome = $RuntimeHomes | Where-Object {
        # Take the first path that isn't under program files
        !($_.StartsWith($pf) -or $_.StartsWith($pf32))
    } | Select-Object -First 1

    _WriteDebug "Found: $UserHome"
    
    if(!$UserHome) {
        $UserHome = "$env:USERPROFILE\$DefaultUserDirectoryName"
    }
}

_WriteDebug ""
_WriteDebug "=== Running $CommandName ==="
_WriteDebug "Runtime Homes: $RuntimeHomes"
_WriteDebug "User Home: $UserHome"
$AliasesDir = Join-Path $UserHome "alias"
$RuntimesDir = Join-Path $UserHome $InstallSubFolder
$GlobalRuntimesDir = Join-Path $GlobalHome $InstallSubFolder
$Aliases = $null

### Helper Functions
# Checks if a specified file exists in the destination folder and if not, copies the file
# to the destination folder. 
function Safe-Filecopy {
    param(
        [Parameter(Mandatory=$true, Position=0)] $Filename, 
        [Parameter(Mandatory=$true, Position=1)] $SourceFolder,
        [Parameter(Mandatory=$true, Position=2)] $DestinationFolder)

    # Make sure the destination folder is created if it doesn't already exist.
    if(!(Test-Path $DestinationFolder)) {
        _WriteOut "Creating destination folder '$DestinationFolder' ... "

        New-Item -Type Directory $Destination | Out-Null
    }

    $sourceFilePath = Join-Path $SourceFolder $Filename
    $destFilePath = Join-Path $DestinationFolder $Filename

    if(Test-Path $sourceFilePath) {
        _WriteOut "Installing '$Filename' to '$DestinationFolder' ... "

        if (Test-Path $destFilePath) {
            _WriteOut "  Skipping: file already exists" -ForegroundColor Yellow
        }
        else {
            Copy-Item $sourceFilePath $destFilePath -Force
        }
    }
    else {
        _WriteOut "WARNING: Unable to install: Could not find '$Filename' in '$SourceFolder'. " 
    }
}

function GetRuntimeInfo($Architecture, $OS, $Version) {
    $runtimeInfo = @{
        "Architecture"="$Architecture";
        "OS"="$OS";
        "Version"="$Version";
    }

    if([String]::IsNullOrEmpty($runtimeInfo.OS)) {
        $runtimeInfo.OS = "win"
    }

    # Normalization
    if($runtimeInfo.OS -eq "darwin") {
        $runtimeInfo.OS = "osx"
    }

    if($runtimeInfo.OS -eq "windows") {
        $runtimeInfo.OS = "win"
    }

    if([String]::IsNullOrEmpty($runtimeInfo.Architecture)) {
        $runtimeInfo.Architecture = "x64"
   }

    $runtimeObject = New-Object PSObject -Property $runtimeInfo

    $runtimeObject | Add-Member -MemberType ScriptProperty -Name RuntimeId -Value {
        "$RuntimePackageName-$($this.OS)-$($this.Architecture)".ToLowerInvariant()
    }

    $runtimeObject | Add-Member -MemberType ScriptProperty -Name RuntimeName -Value {
        "$($this.RuntimeId).$($this.Version)"
    }

    $runtimeObject
}

function Write-Usage {
    _WriteOut -ForegroundColor $ColorScheme.Banner $AsciiArt
    _WriteOut "$CommandFriendlyName v$FullVersion"
    if(!$Authors.StartsWith("{{")) {
        _WriteOut "By $Authors"
    }
    _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Header "usage:"
    _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Executable " $CommandName"
    _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Command " <command>"
    _WriteOut -ForegroundColor $ColorScheme.Help_Argument " [<arguments...>]"
}

function Write-Feeds {
    _WriteOut
    _WriteOut -ForegroundColor $ColorScheme.Help_Header "Current feed settings:"
    _WriteOut -NoNewline -ForegroundColor $ColorScheme.Feed_Name "Default Stable: "
    _WriteOut "$DefaultFeed"
    _WriteOut -NoNewline -ForegroundColor $ColorScheme.Feed_Name "Default Unstable: "
    _WriteOut "$DefaultUnstableFeed"
    _WriteOut -NoNewline -ForegroundColor $ColorScheme.Feed_Name "Current Stable Override: "
    if($ActiveFeed) {
        _WriteOut "$ActiveFeed"
    } else {
        _WriteOut "<none>"
    }
    _WriteOut -NoNewline -ForegroundColor $ColorScheme.Feed_Name "Current Unstable Override: "
    if($ActiveUnstableFeed) {
        _WriteOut "$ActiveUnstableFeed"
    } else {
        _WriteOut "<none>"
    }
    _WriteOut
    _WriteOut -NoNewline "    To use override feeds, set "
    _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Executable "$DefaultFeedKey"
    _WriteOut -NoNewline " and "
    _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Executable "$DefaultUnstableFeedKey"
    _WriteOut -NoNewline " environment keys respectively"
    _WriteOut
}

function Get-RuntimeAlias {
    if($Aliases -eq $null) {
        _WriteDebug "Scanning for aliases in $AliasesDir"
        if(Test-Path $AliasesDir) {
            $Aliases = @(Get-ChildItem ($UserHome + "\alias\") | Select-Object @{label='Alias';expression={$_.BaseName}}, @{label='Name';expression={Get-Content $_.FullName }}, @{label='Orphan';expression={-Not (Test-Path ($RuntimesDir + "\" + (Get-Content $_.FullName)))}})
        } else {
            $Aliases = @()
        }
    }
    $Aliases
}

function IsOnPath {
    param($dir)

    $env:Path.Split(';') -icontains $dir
}

function Get-RuntimeAliasOrRuntimeInfo(
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter()][string]$Architecture,
    [Parameter()][string]$Runtime,
    [Parameter()][string]$OS) {

    $aliasPath = Join-Path $AliasesDir "$Version$AliasExtension"

    if(Test-Path $aliasPath) {
        $BaseName = Get-Content $aliasPath

        if(!$Architecture) {
            $Architecture = Get-PackageArch $BaseName
        }
        if(!$Runtime) {
            $Runtime = Get-PackageRuntime $BaseName
        }
        $Version = Get-PackageVersion $BaseName
        $OS = Get-PackageOS $BaseName
    }

    GetRuntimeInfo $Architecture $OS $Version
}

filter List-Parts {
    param($aliases, $items)

    $location = ""

    if ((Test-Path $_.FullName)) {
        $location = $_.Parent.FullName
    }
    $active = IsOnPath $_.FullName

    $fullAlias=""
    $delim=""

    foreach($alias in $aliases) {
        if($_.Name.Split('\', 2) -contains $alias.Name) {
            $fullAlias += $delim + $alias.Alias + (&{if($alias.Orphan){" (missing)"}})
            $delim = ", "
        }
    }

    $parts1 = $_.Name.Split('.', 2)
    $parts2 = $parts1[0].Split('-', 3)

    $aliasUsed = ""
    if($items) {
    $aliasUsed = $items | ForEach-Object {
        if($_.Architecture -eq $parts2[2] -and $_.OperatingSystem -eq $parts2[1] -and $_.Version -eq $parts1[1]) {
            return $true;
        }
        return $false;
    }
    }

    if($aliasUsed -eq $true) {
        $fullAlias = ""
    }

    return New-Object PSObject -Property @{
        Active = $active
        Version = $parts1[1]
        OperatingSystem = $parts2[1]
        Architecture = $parts2[2]
        Location = $location
        Alias = $fullAlias
    }
}

function Read-Alias($Name) {
    _WriteDebug "Listing aliases matching '$Name'"

    $aliases = Get-RuntimeAlias

    $result = @($aliases | Where-Object { !$Name -or ($_.Alias.Contains($Name)) })
    if($Name -and ($result.Length -eq 1)) {
        _WriteOut "Alias '$Name' is set to '$($result[0].Name)'"
    } elseif($Name -and ($result.Length -eq 0)) {
        _WriteOut "Alias does not exist: '$Name'"
        $Script:ExitCode = $ExitCodes.AliasDoesNotExist
    } else {
        $result
    }
}

function Write-Alias {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [Parameter(Mandatory=$true)][string]$Version,
        [Parameter(Mandatory=$false)][string]$Architecture,
        [Parameter(Mandatory=$false)][string]$Runtime,
        [Parameter(Mandatory=$false)][string]$OS)

    # If the first character is non-numeric, it's a full runtime name
    if(![Char]::IsDigit($Version[0])) {
        $runtimeInfo = GetRuntimeInfo $(Get-PackageArch $Version) $(Get-PackageRuntime $Version) $(Get-PackageOS $Version) $(Get-PackageVersion $Version)
    } else {
        $runtimeInfo = GetRuntimeInfo $Architecture $Runtime $OS $Version
    }

    $aliasFilePath = Join-Path $AliasesDir "$Name.txt"
    $action = if (Test-Path $aliasFilePath) { "Updating" } else { "Setting" }

    if(!(Test-Path $AliasesDir)) {
        _WriteDebug "Creating alias directory: $AliasesDir"
        New-Item -Type Directory $AliasesDir | Out-Null
    }
    _WriteOut "$action alias '$Name' to '$($runtimeInfo.RuntimeName)'"
    $runtimeInfo.RuntimeName | Out-File $aliasFilePath ascii
}

function Delete-Alias {
    param(
        [Parameter(Mandatory=$true)][string]$Name)

    $aliasPath = Join-Path $AliasesDir "$Name.txt"
    if (Test-Path -literalPath "$aliasPath") {
        _WriteOut "Removing alias $Name"

        # Delete with "-Force" because we already confirmed above
        Remove-Item -literalPath $aliasPath -Force
    } else {
        _WriteOut "Cannot remove alias '$Name'. It does not exist."
        $Script:ExitCode = $ExitCodes.AliasDoesNotExist # Return non-zero exit code for scripting
    }
}

function Apply-Proxy {
param(
  [System.Net.WebClient] $wc,
  [string]$Proxy
)
  if (!$Proxy) {
    $Proxy = $env:http_proxy
  }
  if ($Proxy) {
    $wp = New-Object System.Net.WebProxy($Proxy)
    $pb = New-Object UriBuilder($Proxy)
    if (!$pb.UserName) {
        $wp.Credentials = [System.Net.CredentialCache]::DefaultCredentials
    } else {
        $wp.Credentials = New-Object System.Net.NetworkCredential($pb.UserName, $pb.Password)
    }
    $wc.Proxy = $wp
  }
}

function Join-UrlFragments
{
    param
    (
        $Parts = $null
    )

    ($Parts | ? { $_ } | % { ([string]$_).trim("/") } | ? { $_ } ) -join "/" 
}

function Find-Package {
    param(
        $runtimeInfo,
        [Parameter(Mandatory=$true)]
        [string]$Feed,
        [string]$Proxy,
        [string]$channel
    )

    _WriteOut "Determining latest version"
    $RuntimeId = $runtimeInfo.RuntimeId
    _WriteDebug "Latest RuntimeId: $RuntimeId"
    $url = Join-UrlFragments $Feed,$channel,"dnvm","index"
    _WriteDebug "Index URL: $url"

    $wc = New-Object System.Net.WebClient
    Apply-Proxy $wc -Proxy:$Proxy
    _WriteDebug "Downloading $Url ..."
    try {
        $index = $wc.DownloadString($Url)
    } catch {
        $Script:ExitCode = $ExitCodes.NoRuntimesOnFeed
        throw "Unable to find any runtime packages on the feed!"
    }

    if($runtimeInfo.Version -eq "latest") {
        $version = $index | ?{$_ -match "Latest: (?<version>.+)?"} | %{$matches["version"]}
    } else {
        $version = $runtimeInfo.Version
    }

    if($version) {
        $urlPart = $index | ?{$_ -match "Filename: (?<url>.+?$RuntimeId.$version.zip)"} | %{$matches["url"]}
        _WriteDebug "Found Package Path: $urlPart"
        $downloadUrl = Join-UrlFragments $Feed,$channel,$urlPart
        _WriteDebug "Found $version at $downloadUrl"
        @{ Version = $version; DownloadUrl = $downloadUrl }
    } else {
        throw "There are no SDKs matching the name $RuntimeId on channel '$channel'."
    }
}

function Get-PackageVersion() {
    param(
        [string] $runtimeFullName
    )
    return $runtimeFullName -replace '[^.]*.(.*)', '$1'
}

function Get-PackageArch() {
    param(
        [string] $runtimeFullName
    )
    return $runtimeFullName -replace "$RuntimePackageName-[^-]*-([^.]*).*", '$1'
}

function Get-PackageOS() {
    param(
        [string] $runtimeFullName
    )
    $runtimeFullName -replace "$RuntimePackageName-([^-]*)-[^.]*.*", '$1'
}

function Download-Package() {
    param(
        $runtimeInfo,
        [Parameter(Mandatory=$true)]
        [string]$DownloadUrl,
        [string]$DestinationFile,
        [Parameter(Mandatory=$true)]
        [string]$Feed,
        [string]$Proxy
    )

    _WriteOut "Downloading $($runtimeInfo.RuntimeName) from $feed"
    $wc = New-Object System.Net.WebClient
    try {
      Apply-Proxy $wc -Proxy:$Proxy
      _WriteDebug "Downloading $DownloadUrl ..."

      Register-ObjectEvent $wc DownloadProgressChanged -SourceIdentifier WebClient.ProgressChanged -action {
        $Global:downloadData = $eventArgs
      } | Out-Null

      Register-ObjectEvent $wc DownloadFileCompleted -SourceIdentifier WebClient.ProgressComplete -action {
        $Global:downloadData = $eventArgs
        $Global:downloadCompleted = $true
      } | Out-Null

      $wc.DownloadFileAsync($DownloadUrl, $DestinationFile)

      while(-not $Global:downloadCompleted){
        $percent = $Global:downloadData.ProgressPercentage
        $totalBytes = $Global:downloadData.TotalBytesToReceive
        $receivedBytes = $Global:downloadData.BytesReceived
        If ($percent -ne $null) {
            Write-Progress -Activity ("Downloading $RuntimeShortFriendlyName from $DownloadUrl") `
                -Status ("Downloaded $($Global:downloadData.BytesReceived) of $($Global:downloadData.TotalBytesToReceive) bytes") `
                -PercentComplete $percent -Id 2 -ParentId 1
        }
      }

      if($Global:downloadData.Error) {
        if($Global:downloadData.Error.Response.StatusCode -eq [System.Net.HttpStatusCode]::NotFound){
            throw "The server returned a 404 (NotFound). This is most likely caused by the feed not having the version that you typed. Check that you typed the right version and try again. Other possible causes are the feed doesn't have a $RuntimeShortFriendlyName of the right name format or some other error caused a 404 on the server."
        } else {
            throw "Unable to download package: {0}" -f $Global:downloadData.Error.Message
        }
      }

      Write-Progress -Status "Done" -Activity ("Downloading $RuntimeShortFriendlyName from $DownloadUrl") -Id 2 -ParentId 1 -Completed
    }
    finally {
        Remove-Variable downloadData -Scope "Global"
        Remove-Variable downloadCompleted -Scope "Global"
        Unregister-Event -SourceIdentifier WebClient.ProgressChanged
        Unregister-Event -SourceIdentifier WebClient.ProgressComplete
        $wc.Dispose()
    }
}

function Unpack-Package([string]$DownloadFile, [string]$UnpackFolder) {
    _WriteDebug "Unpacking $DownloadFile to $UnpackFolder"

    $compressionLib = [System.Reflection.Assembly]::LoadWithPartialName('System.IO.Compression.FileSystem')

    try {
        if($compressionLib -eq $null) {
            # Use the shell to uncompress the zip
            $shell_app=new-object -com shell.application
            $zip_file = $shell_app.namespace($DownloadFile)
            $destination = $shell_app.namespace($UnpackFolder)
            $destination.Copyhere($zip_file.items(), 0x14) #0x4 = don't show UI, 0x10 = overwrite files
        } else {
            [System.IO.Compression.ZipFile]::ExtractToDirectory($DownloadFile, $UnpackFolder)
        }
    } finally {
        # Clean up the package file itself.
        Remove-Item $DownloadFile -Force
    }

    #NOTE: This can be removed as soon as we start re-packing the zips on blob storage.
    #for now they are just renamed so we will continue to do this but we can remove it in the near future.
    If (Test-Path -LiteralPath ($UnpackFolder + "\[Content_Types].xml")) {
        Remove-Item -LiteralPath ($UnpackFolder + "\[Content_Types].xml")
    }
    If (Test-Path ($UnpackFolder + "\_rels\")) {
        Remove-Item -LiteralPath ($UnpackFolder + "\_rels\") -Force -Recurse
    }
    If (Test-Path ($UnpackFolder + "\package\")) {
        Remove-Item -LiteralPath ($UnpackFolder + "\package\") -Force -Recurse
    }
}

function Get-RuntimePath($runtimeFullName) {
    _WriteDebug "Resolving $runtimeFullName"
    foreach($RuntimeHome in $RuntimeHomes) {
        $runtimeBin = "$RuntimeHome\$InstallSubfolder\$runtimeFullName"
        _WriteDebug " Candidate $runtimeBin"
        if (Test-Path $runtimeBin) {
            _WriteDebug " Found in $runtimeBin"
            return $runtimeBin
        }
    }
    return $null
}

function Change-Path() {
    param(
        [string] $existingPaths,
        [string] $prependPath,
        [string[]] $removePaths
    )
    _WriteDebug "Updating value to prepend '$prependPath' and remove '$removePaths'"

    $newPath = $prependPath
    foreach($portion in $existingPaths.Split(';')) {
        if(![string]::IsNullOrEmpty($portion)) {
            $skip = $portion -eq ""
            foreach($removePath in $removePaths) {
                if(![string]::IsNullOrEmpty($removePath)) {
                    $removePrefix = if($removePath.EndsWith("\")) { $removePath } else { "$removePath\" }

                    if ($removePath -and (($portion -eq $removePath) -or ($portion.StartsWith($removePrefix)))) {
                        _WriteDebug " Removing '$portion' because it matches '$removePath'"
                        $skip = $true
                    }
                }
            }
            if (!$skip) {
                if(![String]::IsNullOrEmpty($newPath)) {
                    $newPath += ";"
                }
                $newPath += $portion
            }
        }
    }
    return $newPath
}

function Set-Path() {
    param(
        [string] $newPath
    )

    $env:PATH = $newPath

    if($CmdPathFile) {
        $Parent = Split-Path -Parent $CmdPathFile
        if(!(Test-Path $Parent)) {
            New-Item -Type Directory $Parent -Force | Out-Null
        }
        _WriteDebug " Writing PATH file for CMD script"
        @"
SET "PATH=$newPath"
"@ | Out-File $CmdPathFile ascii
    }
}

function Ngen-Library(
    [Parameter(Mandatory=$true)]
    [string]$runtimeBin,

    [ValidateSet("x86", "x64")]
    [Parameter(Mandatory=$true)]
    [string]$architecture) {

    if ($architecture -eq 'x64') {
        $regView = [Microsoft.Win32.RegistryView]::Registry64
    }
    elseif ($architecture -eq 'x86') {
        $regView = [Microsoft.Win32.RegistryView]::Registry32
    }
    else {
        _WriteOut "Installation does not understand architecture $architecture, skipping ngen..."
        return
    }

    $regHive = [Microsoft.Win32.RegistryHive]::LocalMachine
    $regKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($regHive, $regView)
    $frameworkPath = $regKey.OpenSubKey("SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full").GetValue("InstallPath")
    $ngenExe = Join-Path $frameworkPath 'ngen.exe'

    $ngenCmds = ""
    foreach ($bin in Get-ChildItem $runtimeBin -Filter "Microsoft.CodeAnalysis.CSharp.dll") {
        $ngenCmds += "$ngenExe install $($bin.FullName);"
    }

    $ngenProc = Start-Process "$psHome\powershell.exe" -Verb runAs -ArgumentList "-ExecutionPolicy unrestricted & $ngenCmds" -Wait -PassThru -WindowStyle Hidden
}

function Is-Elevated() {
    $user = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
    return $user.IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
}

### Commands

<#
.SYNOPSIS
    Updates DNVM to the latest version.
.PARAMETER Proxy
    Use the given address as a proxy when accessing remote server
#>
function dnvm-update-self {
    param(
        [Parameter(Mandatory=$false)] 
        [string]$Proxy)

    _WriteOut "Updating $CommandName from $DNVMUpgradeUrl"
    $wc = New-Object System.Net.WebClient
    Apply-Proxy $wc -Proxy:$Proxy

    $dnvmFile = Join-Path $PSScriptRoot "dnvm.ps1"
    $tempDnvmFile = Join-Path $PSScriptRoot "temp"
    $backupFilePath = Join-Path $PSSCriptRoot "dnvm.ps1.bak"

    $wc.DownloadFile($DNVMUpgradeUrl, $tempDnvmFile)

    if(Test-Path $backupFilePath) {
        Remove-Item $backupFilePath -Force
    }

    Rename-Item $dnvmFile $backupFilePath
    Rename-Item $tempDnvmFile $dnvmFile
}

<#
.SYNOPSIS
    Displays a list of commands, and help for specific commands
.PARAMETER Command
    A specific command to get help for
#>
function dnvm-help {
    [CmdletBinding(DefaultParameterSetName="GeneralHelp")]
    param(
        [Parameter(Mandatory=$true,Position=0,ParameterSetName="SpecificCommand")][string]$Command,
        [switch]$PassThru)

    if($Command) {
        $cmd = Get-Command "dnvm-$Command" -ErrorAction SilentlyContinue
        if(!$cmd) {
            _WriteOut "No such command: $Command"
            dnvm-help
            $Script:ExitCodes = $ExitCodes.UnknownCommand
            return
        }
        if($Host.Version.Major -lt 3) {
            $help = Get-Help "dnvm-$Command"
        } else {
            $help = Get-Help "dnvm-$Command" -ShowWindow:$false
        }
        if($PassThru -Or $Host.Version.Major -lt 3) {
            $help
        } else {
            _WriteOut -ForegroundColor $ColorScheme.Help_Header "$CommandName $Command"
            _WriteOut "  $($help.Synopsis.Trim())"
            _WriteOut
            _WriteOut -ForegroundColor $ColorScheme.Help_Header "usage:"
            $help.Syntax.syntaxItem | ForEach-Object {
                _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Executable "  $CommandName "
                _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Command "$Command"
                if($_.parameter) {
                    $_.parameter | ForEach-Object {
                        $cmdParam = $cmd.Parameters[$_.name]
                        $name = $_.name
                        if($cmdParam.Aliases.Length -gt 0) {
                            $name = $cmdParam.Aliases | Sort-Object | Select-Object -First 1
                        }

                        _WriteOut -NoNewLine " "

                        if($_.required -ne "true") {
                            _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Optional "["
                        }

                        if($_.position -eq "Named") {
                            _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Switch "-$name"
                        }
                        if($_.parameterValue) {
                            if($_.position -eq "Named") {
                                _WriteOut -NoNewLine " "
                            }
                            _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Argument "<$($_.name)>"
                        }

                        if($_.required -ne "true") {
                            _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Optional "]"
                        }
                    }
                }
                _WriteOut
            }

            if($help.parameters -and $help.parameters.parameter) {
                _WriteOut
                _WriteOut -ForegroundColor $ColorScheme.Help_Header "options:"
                $help.parameters.parameter | ForEach-Object {
                    $cmdParam = $cmd.Parameters[$_.name]
                    $name = $_.name
                    if($cmdParam.Aliases.Length -gt 0) {
                        $name = $cmdParam.Aliases | Sort-Object | Select-Object -First 1
                    }

                    _WriteOut -NoNewLine "  "

                    if($_.position -eq "Named") {
                        _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Switch "-$name".PadRight($OptionPadding)
                    } else {
                        _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Argument "<$($_.name)>".PadRight($OptionPadding)
                    }
                    _WriteOut " $($_.description.Text)"
                }
            }

            if($help.description) {
                _WriteOut
                _WriteOut -ForegroundColor $ColorScheme.Help_Header "remarks:"
                $help.description.Text.Split(@("`r", "`n"), "RemoveEmptyEntries") | 
                    ForEach-Object { _WriteOut "  $_" }
            }

            if($DeprecatedCommands -contains $Command) {
                _WriteOut "This command has been deprecated and should not longer be used"
            }
        }
    } else {
        Write-Usage
        Write-Feeds
        _WriteOut
        _WriteOut -ForegroundColor $ColorScheme.Help_Header "commands: "
        Get-Command "$CommandPrefix*" | 
            ForEach-Object {
                if($Host.Version.Major -lt 3) {
                    $h = Get-Help $_.Name
                } else {
                    $h = Get-Help $_.Name -ShowWindow:$false
                }
                $name = $_.Name.Substring($CommandPrefix.Length)
                if($DeprecatedCommands -notcontains $name) {
                    _WriteOut -NoNewLine "    "
                    _WriteOut -NoNewLine -ForegroundColor $ColorScheme.Help_Command $name.PadRight($CommandPadding)
                    _WriteOut " $($h.Synopsis.Trim())"
                }
            }
    }
}

filter ColorActive {
    param([string] $color)
    $lines = $_.Split("`n")
    foreach($line in $lines) {
        if($line.Contains("*")){
            _WriteOut -ForegroundColor $ColorScheme.ActiveRuntime $line 
        } else {
            _WriteOut $line
        }
    }
}

<#
.SYNOPSIS
    Displays the DNVM version.
#>
function dnvm-version {
    _WriteOut "$FullVersion"
}

<#
.SYNOPSIS
    Lists available SDKs
.PARAMETER Detailed
    Display more detailed information on each runtime
.PARAMETER PassThru
    Set this switch to return unformatted powershell objects for use in scripting
#>
function dnvm-list {
    param(
        [Parameter(Mandatory=$false)][switch]$PassThru,
        [Parameter(Mandatory=$false)][switch]$Detailed)
    $aliases = Get-RuntimeAlias

    if(-not $PassThru) {
        Check-Runtimes
    }

    $items = @()
    $RuntimeHomes | ForEach-Object {
        _WriteDebug "Scanning $_ for SDKs..."
        if (Test-Path "$_\$InstallSubfolder") {
            $items += Get-ChildItem "$_\$InstallSubfolder\$RuntimePackageName-*" | List-Parts $aliases $items
        }
    }

    $aliases | Where-Object {$_.Orphan} | ForEach-Object {
        $items += $_ | Select-Object @{label='Name';expression={$_.Name}}, @{label='FullName';expression={Join-Path $RuntimesDir $_.Name}} | List-Parts $aliases
    }

    if($PassThru) {
        $items
    } else {
        if($items) {
            #TODO: Probably a better way to do this.
            if($Detailed) {
                $items | 
                    Sort-Object Version, Alias | 
                    Format-Table -AutoSize -Property @{name="Active";expression={if($_.Active) { "*" } else { "" }};alignment="center"}, "Version", "Alias", "Location" | Out-String| ColorActive
            } else {
                $items | 
                    Sort-Object Version, Architecture, OperatingSystem, Alias | 
                    Format-Table -AutoSize -Property @{name="Active";expression={if($_.Active) { "*" } else { "" }};alignment="center"}, "Version", "Alias" | Out-String | ColorActive
            }
        } else {
            _WriteOut "No SDKs installed. You can run 'dnvm install latest' or 'dnvm upgrade' to install a runtime."
        }
    }
}

<#
.SYNOPSIS
    Lists and manages aliases
.PARAMETER Name
    The name of the alias to read/create/delete
.PARAMETER Version
    The version to assign to the new alias
.PARAMETER Architecture
    The architecture of the runtime to assign to this alias
.PARAMETER OS
    The operating system that the runtime targets
.PARAMETER Delete
    Set this switch to delete the alias with the specified name
.DESCRIPTION
    If no arguments are provided, this command lists all aliases. If <Name> is provided,
    the value of that alias, if present, is displayed. If <Name> and <Version> are
    provided, the alias <Name> is set to the runtime defined by <Version>, <Architecture>
    (defaults to 'x86') and <Runtime> (defaults to 'clr').

    Finally, if the '-d' switch is provided, the alias <Name> is deleted, if it exists.

    NOTE: You cannot create an alias for a non-windows runtime. The intended use case for
    an alias to help make it easier to switch the runtime, and you cannot use a non-windows
    runtime on a windows machine.
#>
function dnvm-alias {
    param(
        [Alias("d")]
        [switch]$Delete,

        [Parameter(Position=0)]
        [string]$Name,

        [Parameter(Position=1)]
        [string]$Version,

        [Alias("arch")]
        [ValidateSet("", "x86", "x64", "arm")]
        [string]$Architecture = "",

        [ValidateSet("win", "osx", "linux")]
        [Parameter(Mandatory=$false,ParameterSetName="Write")]
        [string]$OS = "")

    if($Name -like "help" -or $Name -like "/?") {
        #It is unlikely that the user is trying to read an alias called help, so lets just help them out by displaying help text.
        #If people need an alias called help or one that contains a `?` then we can change this to a prompt.
        dnvm help alias
        return
    }

    if($Version) {
        Write-Alias $Name $Version -Architecture $Architecture -OS:$OS
    } elseif ($Delete) {
        Delete-Alias $Name
    } else {
        Read-Alias $Name
    }
}

<#
.SYNOPSIS
    Installs the latest version of the runtime and reassigns the specified alias to point at it
.PARAMETER Alias
    The alias to upgrade (default: 'default')
.PARAMETER Architecture
    The processor architecture of the runtime to install (default: x86)
.PARAMETER OS
    The operating system that the runtime targets (default: win)
.PARAMETER Force
    Overwrite an existing runtime if it already exists
.PARAMETER Proxy
    Use the given address as a proxy when accessing remote server
.PARAMETER NoNative
    Skip generation of native images
.PARAMETER Ngen
    For CLR flavor only. Generate native images for runtime libraries on Desktop CLR to improve startup time. This option requires elevated privilege and will be automatically turned on if the script is running in administrative mode. To opt-out in administrative mode, use -NoNative switch.
.PARAMETER Unstable
    Upgrade from the unstable dev feed. This will give you the latest development version of the runtime. 
.PARAMETER Global
    Installs to configured global dnx file location (default: C:\ProgramData)
#>
function dnvm-upgrade {
    param(
        [Alias("a")]
        [Parameter(Mandatory=$false, Position=0)]
        [string]$Alias = "default",

        [Alias("arch")]
        [ValidateSet("", "x86", "x64", "arm")]
        [Parameter(Mandatory=$false)]
        [string]$Architecture = "",

        [ValidateSet("", "win", "osx", "linux")]
        [Parameter(Mandatory=$false)]
        [string]$OS = "",

        [Alias("f")]
        [Parameter(Mandatory=$false)]
        [switch]$Force,

        [Parameter(Mandatory=$false)]
        [string]$Proxy,

        [Parameter(Mandatory=$false)]
        [switch]$NoNative=$true,

        [Parameter(Mandatory=$false)]
        [switch]$Ngen,

        [Parameter(Mandatory=$false)]
        [switch]$Unstable,

        [Parameter(Mandatory=$false)]
        [switch]$Global)

    if($OS -ne "win" -and ![String]::IsNullOrEmpty($OS)) {
        #We could remove OS as an option from upgrade, but I want to take this opporunty to educate users about the difference between install and upgrade
        #It's possible we should just do install here instead.
         _WriteOut -ForegroundColor $ColorScheme.Error "You cannot upgrade to a non-windows runtime. Upgrade will download the latest version of the $RuntimeShortFriendlyName and also set it as your machines default. You cannot set the default $RuntimeShortFriendlyName to a non-windows version because you cannot use it to run an application. If you want to install a non-windows $RuntimeShortFriendlyName to package with your application then use 'dnvm install latest -OS:$OS' instead. Install will download the package but not set it as your default."
        $Script:ExitCode = $ExitCodes.OtherError
        return
    }

    dnvm-install "latest" -Alias:$Alias -Architecture:$Architecture -OS:$OS -Force:$Force -Proxy:$Proxy -NoNative:$NoNative -Ngen:$Ngen -Unstable:$Unstable -Persistent:$true -Global:$Global
}

<#
.SYNOPSIS
    Installs a version of the runtime
.PARAMETER VersionNuPkgOrAlias
    The version to install from the current channel, the path to a '.nupkg' file to install, 'latest' to
    install the latest available version from the current channel, or an alias value to install an alternate
    runtime or architecture flavor of the specified alias.
.PARAMETER Architecture
    The processor architecture of the runtime to install (default: x86)
.PARAMETER OS
    The operating system that the runtime targets (default: win)
.PARAMETER Alias
    Set alias <Alias> to the installed runtime
.PARAMETER Force
    Overwrite an existing runtime if it already exists
.PARAMETER Proxy
    Use the given address as a proxy when accessing remote server
.PARAMETER NoNative
    Skip generation of native images
.PARAMETER Ngen
    For CLR flavor only. Generate native images for runtime libraries on Desktop CLR to improve startup time. This option requires elevated privilege and will be automatically turned on if the script is running in administrative mode. To opt-out in administrative mode, use -NoNative switch.
.PARAMETER Persistent
    Make the installed runtime useable across all processes run by the current user
.PARAMETER Unstable
    Upgrade from the unstable dev feed. This will give you the latest development version of the runtime.
.PARAMETER Global
    Installs to configured global dnx file location (default: C:\ProgramData)
.DESCRIPTION
    A proxy can also be specified by using the 'http_proxy' environment variable
#>
function dnvm-install {
    param(
        [Parameter(Mandatory=$false, Position=0)]
        [string]$VersionNuPkgOrAlias,

        [Alias("arch")]
        [ValidateSet("", "x86", "x64", "arm")]
        [Parameter(Mandatory=$false)]
        [string]$Architecture = "",

        [ValidateSet("", "win", "osx", "linux")]
        [Parameter(Mandatory=$false)]
        [string]$OS = "",

        [Alias("a")]
        [Parameter(Mandatory=$false)]
        [string]$Alias,

        [Alias("f")]
        [Parameter(Mandatory=$false)]
        [switch]$Force,

        [Parameter(Mandatory=$false)]
        [string]$Proxy,

        [Parameter(Mandatory=$false)]
        [switch]$NoNative,

        [Parameter(Mandatory=$false)]
        [switch]$Ngen,

        [Parameter(Mandatory=$false)]
        [switch]$Persistent,

        [Parameter(Mandatory=$false)]
        [switch]$Unstable,

        [Parameter(Mandatory=$false)]
        [switch]$Global)

    $selectedFeed = ""
    $activeChannel = ""
    #This will change to a more first class channels feature in the future.
    if($Unstable) {
        $selectedFeed = $ActiveUnstableFeed
        if(!$selectedFeed) {
            $selectedFeed = $DefaultUnstableFeed
        } else {
            _WriteOut -ForegroundColor $ColorScheme.Warning "Default unstable feed ($DefaultUnstableFeed) is being overridden by the value of the $DefaultUnstableFeedKey environment variable ($ActiveUnstableFeed)"
        }
        $activeChannel="dev"
    } else {
        $selectedFeed = $ActiveFeed
        if(!$selectedFeed) {
            $selectedFeed = $DefaultFeed
        } else {
            _WriteOut -ForegroundColor $ColorScheme.Warning "Default stable feed ($DefaultFeed) is being overridden by the value of the $DefaultFeedKey environment variable ($ActiveFeed)"
        }
        $activeChannel="dev"
    }

    if(!$VersionNuPkgOrAlias) {
        _WriteOut "A version, nupkg path, or the string 'latest' must be provided."
        dnvm-help install
        $Script:ExitCode = $ExitCodes.InvalidArguments
        return
    }

    $IsNuPkg = $VersionNuPkgOrAlias.EndsWith(".nupkg")

    if ($IsNuPkg) {
        if(!(Test-Path $VersionNuPkgOrAlias)) {
            throw "Unable to locate package file: '$VersionNuPkgOrAlias'"
        }
        Write-Progress -Activity "Installing runtime" -Status "Parsing package file name" -Id 1
        $runtimeFullName = [System.IO.Path]::GetFileNameWithoutExtension($VersionNuPkgOrAlias)
        $Architecture = Get-PackageArch $runtimeFullName
        $OS = Get-PackageOS $runtimeFullName
        $Version = Get-PackageVersion $runtimeFullName
    } else {
        $aliasPath = Join-Path $AliasesDir "$VersionNuPkgOrAlias$AliasExtension"
        if(Test-Path $aliasPath) {
            $BaseName = Get-Content $aliasPath
            #Check empty checks let us override a given alias property when installing the same again. e.g. `dnvm install default -x64`
            if([String]::IsNullOrEmpty($Architecture)) {
                $Architecture = Get-PackageArch $BaseName
            }

            if([String]::IsNullOrEmpty($Version)) {
                $Version = Get-PackageVersion $BaseName
            }

            if([String]::IsNullOrEmpty($OS)) {
                $OS = Get-PackageOS $BaseName
            }
        } else {
            $Version = $VersionNuPkgOrAlias
        }
    }

    $runtimeInfo = GetRuntimeInfo $Architecture $OS $Version

    if (!$IsNuPkg) {
        $findPackageResult = Find-Package -runtimeInfo:$runtimeInfo -Feed:$selectedFeed -Channel:$activeChannel
        $Version = $findPackageResult.Version
    }

    #If the version is still empty at this point then VersionOrNupkgOrAlias is an actual version.
    if([String]::IsNullOrEmpty($Version)) {
        $Version = $VersionNuPkgOrAlias
    }

    $runtimeInfo.Version = $Version

    _WriteDebug "Preparing to install runtime '$($runtimeInfo.RuntimeName)'"
    _WriteDebug "Architecture: $($runtimeInfo.Architecture)"
    _WriteDebug "Version: $($runtimeInfo.Version)"
    _WriteDebug "OS: $($runtimeInfo.OS)"

    $installDir = $RuntimesDir
    if (!$Global) {
        $RuntimeFolder = Join-Path $RuntimesDir $($runtimeInfo.RuntimeName)
    }
    else {
        $installDir = $GlobalRuntimesDir
        $RuntimeFolder = Join-Path $GlobalRuntimesDir $($runtimeInfo.RuntimeName)
    }

    _WriteDebug "Destination: $RuntimeFolder"

    if((Test-Path $RuntimeFolder) -and $Force) {
        _WriteOut "Cleaning existing installation..."
        Remove-Item $RuntimeFolder -Recurse -Force
    }

    $installed=""
    if(Test-Path (Join-Path $RuntimesDir $($runtimeInfo.RuntimeName))) {
        $installed = Join-Path $RuntimesDir $($runtimeInfo.RuntimeName)
    }
    if(Test-Path (Join-Path $GlobalRuntimesDir $($runtimeInfo.RuntimeName))) {
        $installed = Join-Path $GlobalRuntimesDir $($runtimeInfo.RuntimeName)
    }
    if($installed -ne "") {
        _WriteOut "'$($runtimeInfo.RuntimeName)' is already installed in $installed."
        if($runtimeInfo.OS -eq "win") {
            dnvm-use $runtimeInfo.Version -Architecture:$runtimeInfo.Architecture -Persistent:$Persistent -OS:$runtimeInfo.OS
        }
    }
    else {
        $Architecture = $runtimeInfo.Architecture
        $OS = $runtimeInfo.OS

        $TempFolder = Join-Path $installDir "temp" 
        $UnpackFolder = Join-Path $TempFolder $runtimeInfo.RuntimeName
        $DownloadFile = Join-Path $UnpackFolder "$($runtimeInfo.RuntimeName).nupkg"

        if(Test-Path $UnpackFolder) {
            _WriteDebug "Cleaning temporary directory $UnpackFolder"
            Remove-Item $UnpackFolder -Recurse -Force
        }
        New-Item -Type Directory $UnpackFolder | Out-Null

        if($IsNuPkg) {
            Write-Progress -Activity "Installing runtime" -Status "Copying package" -Id 1
            _WriteDebug "Copying local nupkg $VersionNuPkgOrAlias to $DownloadFile"
            Copy-Item $VersionNuPkgOrAlias $DownloadFile
        } else {
            # Download the package
            Write-Progress -Activity "Installing runtime" -Status "Downloading runtime" -Id 1
            _WriteDebug "Downloading version $($runtimeInfo.Version) to $DownloadFile"
            Download-Package -RuntimeInfo:$runtimeInfo -DownloadUrl:$findPackageResult.DownloadUrl -DestinationFile:$DownloadFile -Proxy:$Proxy -Feed:$selectedFeed
        }

        Write-Progress -Activity "Installing runtime" -Status "Unpacking runtime" -Id 1
        Unpack-Package $DownloadFile $UnpackFolder

        if(Test-Path $RuntimeFolder) {
            # Ensure the runtime hasn't been installed in the time it took to download the package.
            _WriteOut "'$($runtimeInfo.RuntimeName)' is already installed."
        }
        else {
            _WriteOut "Installing to $RuntimeFolder"
            _WriteDebug "Moving package contents to $RuntimeFolder"
            try {
                Move-Item $UnpackFolder $RuntimeFolder
            } catch {
                if(Test-Path $RuntimeFolder) {
                    #Attempt to cleanup the runtime folder if it is there after a fail.
                    Remove-Item $RuntimeFolder -Recurse -Force
                    throw
                }
            }
            #If there is nothing left in the temp folder remove it. There could be other installs happening at the same time as this.
            if(Test-Path $(Join-Path $TempFolder "*")) {
                Remove-Item $TempFolder -Recurse
            }
        }

        if($runtimeInfo.OS -eq "win") {
            dnvm-use $runtimeInfo.Version -Architecture:$runtimeInfo.Architecture -Persistent:$Persistent -OS:$runtimeInfo.OS
        }
    }

    if($Alias) {
        if($runtimeInfo.OS -eq "win") {
            _WriteDebug "Aliasing installed runtime to '$Alias'"
            dnvm-alias $Alias $runtimeInfo.Version -Architecture:$RuntimeInfo.Architecture -OS:$RuntimeInfo.OS
        } else {
            _WriteOut "Unable to set an alias for a non-windows runtime. Installing non-windows SDKs on Windows are meant only for publishing, not running."
        }
    }

    Write-Progress -Status "Done" -Activity "Install complete" -Id 1 -Complete
}


<#
.SYNOPSIS
    Adds a runtime to the PATH environment variable for your current shell
.PARAMETER VersionOrAlias
    The version or alias of the runtime to place on the PATH
.PARAMETER Architecture
    The processor architecture of the runtime to place on the PATH (default: x86, or whatever the alias specifies in the case of use-ing an alias)
.PARAMETER OS
    The operating system that the runtime targets (default: win)
.PARAMETER Persistent
    Make the change persistent across all processes run by the current user
#>
function dnvm-use {
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$VersionOrAlias,

        [Alias("arch")]
        [ValidateSet("", "x86", "x64", "arm")]
        [Parameter(Mandatory=$false)]
        [string]$Architecture = "",

        [ValidateSet("", "win", "osx", "darwin", "linux")]
        [Parameter(Mandatory=$false)]
        [string]$OS = "",

        [Alias("p")]
        [Parameter(Mandatory=$false)]
        [switch]$Persistent)

    if ($versionOrAlias -eq "none") {
        _WriteOut "Removing all SDKs from process PATH"
        Set-Path (Change-Path $env:Path "" ($RuntimeDirs))

        return;
    }

    $runtimeInfo = Get-RuntimeAliasOrRuntimeInfo -Version:$VersionOrAlias -Architecture:$Architecture -OS:$OS
    $runtimeFullName = $runtimeInfo.RuntimeName
    $runtimeBin = Get-RuntimePath $runtimeFullName
    _WriteDebug "Using: $runtimeFullName"
    if ($runtimeBin -eq $null) {
        throw "Cannot find $runtimeFullName, do you need to run '$CommandName install $versionOrAlias'?"
    }

    _WriteOut "Adding $runtimeBin to process PATH"
    Set-Path (Change-Path $env:Path $runtimeBin ($RuntimeDirs))
}

<#
.SYNOPSIS
    Locates the dnx.exe for the specified version or alias and executes it, providing the remaining arguments to dnx.exe
.PARAMETER VersionOrAlias
    The version of alias of the runtime to execute
.PARAMETER DnxArguments
    The arguments to pass to dnx.exe
#>
function dnvm-run {
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$VersionOrAlias,

        [Alias("arch")]
        [ValidateSet("", "x86", "x64", "arm")]
        [Parameter(Mandatory=$false)]
        [string]$Architecture = "",

        [Parameter(Mandatory=$false, Position=1, ValueFromRemainingArguments=$true)]
        [object[]]$DnxArguments)

    $runtimeInfo = Get-RuntimeAliasOrRuntimeInfo -Version:$VersionOrAlias -Architecture:$Architecture

    $runtimeBin = Get-RuntimePath $runtimeInfo.RuntimeName
    if ($runtimeBin -eq $null) {
        throw "Cannot find $($runtimeInfo.Name), do you need to run '$CommandName install $versionOrAlias'?"
    }
    $dnxExe = Join-Path $runtimeBin "dnx.exe"
    if(!(Test-Path $dnxExe)) {
        throw "Cannot find a dnx.exe in $runtimeBin, the installation may be corrupt. Try running 'dnvm install $VersionOrAlias -f' to reinstall it"
    }
    _WriteDebug "> $dnxExe $DnxArguments"
    & $dnxExe @DnxArguments
    $Script:ExitCode = $LASTEXITCODE
}

<#
.SYNOPSIS
    Executes the specified command in a sub-shell where the PATH has been augmented to include the specified DNX
.PARAMETER VersionOrAlias
    The version of alias of the runtime to make active in the sub-shell
.PARAMETER Command
    The command to execute in the sub-shell
#>
function dnvm-exec {
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$VersionOrAlias,
        [Parameter(Mandatory=$false, Position=1)]
        [string]$Command,

        [Alias("arch")]
        [ValidateSet("", "x86", "x64", "arm")]
        [Parameter(Mandatory=$false)]
        [string]$Architecture = "",

        [Parameter(Mandatory=$false, Position=2, ValueFromRemainingArguments=$true)]
        [object[]]$Arguments)

    $runtimeInfo = Get-RuntimeAliasOrRuntimeInfo -Version:$VersionOrAlias -Architecture:$Architecture
    $runtimeBin = Get-RuntimePath $runtimeInfo.RuntimeName

    if ($runtimeBin -eq $null) {
        throw "Cannot find $($runtimeInfo.RuntimeName), do you need to run '$CommandName install $versionOrAlias'?"
    }

    $oldPath = $env:PATH
    try {
        $env:PATH = "$runtimeBin;$($env:PATH)"
        & $Command @Arguments
    } finally {
        $Script:ExitCode = $LASTEXITCODE
        $env:PATH = $oldPath
    }
}

<#
.SYNOPSIS
    Installs the version manager into your User profile directory
.PARAMETER SkipUserEnvironmentInstall
    Set this switch to skip configuring the user-level DOTNET_HOME and PATH environment variables
#>
function dnvm-setup {
    param(
        [switch]$SkipUserEnvironmentInstall)

    $DestinationHome = "$env:USERPROFILE\$DefaultUserDirectoryName"

    # Install scripts
    $Destination = "$DestinationHome\bin"
    _WriteOut "Installing $CommandFriendlyName to $Destination"

    $ScriptFolder = Split-Path -Parent $ScriptPath

    # Copy script files (if necessary):
    Safe-Filecopy "$CommandName.ps1" $ScriptFolder $Destination
    Safe-Filecopy "$CommandName.cmd" $ScriptFolder $Destination

    # Configure Environment Variables
    # Also, clean old user home values if present
    # We'll be removing any existing homes, both
    $PathsToRemove = @(
        "%USERPROFILE%\$DefaultUserDirectoryName",
        [Environment]::ExpandEnvironmentVariables($OldUserHome),
        $DestinationHome,
        $OldUserHome)

    # First: PATH
    _WriteOut "Adding $Destination to Process PATH"
    Set-Path (Change-Path $env:PATH $Destination $PathsToRemove)

    if(!$SkipUserEnvironmentInstall) {
        #_WriteOut "Adding $Destination to User PATH"
        #$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
        #$userPath = Change-Path $userPath $Destination $PathsToRemove
        #[Environment]::SetEnvironmentVariable("PATH", $userPath, "User")
    }

    # Now the HomeEnvVar
    _WriteOut "Adding $DestinationHome to Process $HomeEnvVar"
    $processHome = ""
    if(Test-Path "env:\$HomeEnvVar") {
        $processHome = Get-Content "env:\$HomeEnvVar"
    }
    $processHome = Change-Path $processHome "%USERPROFILE%\$DefaultUserDirectoryName" $PathsToRemove
    Set-Content "env:\$HomeEnvVar" $processHome

    if(!$SkipUserEnvironmentInstall) {
        _WriteOut "Adding $DestinationHome to User $HomeEnvVar"
        $userHomeVal = [Environment]::GetEnvironmentVariable($HomeEnvVar, "User")
        $userHomeVal = Change-Path $userHomeVal "%USERPROFILE%\$DefaultUserDirectoryName" $PathsToRemove
        [Environment]::SetEnvironmentVariable($HomeEnvVar, $userHomeVal, "User")
    }
}

function Check-Runtimes(){
    $runtimesInstall = $false;
    foreach($runtimeHomeDir in $RuntimeHomes) {
        if (Test-Path "$runtimeHomeDir\$InstallSubfolder") {
            if(Test-Path "$runtimeHomeDir\$InstallSubfolder\$RuntimePackageName-*"){
                $runtimesInstall = $true;
                break;
            }
        }
    }

    if (-not $runtimesInstall){
        $title = "Getting started"
        $message = "It looks like you don't have any SDKs installed. Do you want us to install a $RuntimeShortFriendlyName to get you started?"

        $yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes", "Install the latest runtime for you"

        $no = New-Object System.Management.Automation.Host.ChoiceDescription "&No", "Do not install the latest runtime and continue"

        $options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)

        $result = $host.ui.PromptForChoice($title, $message, $options, 0)

        if($result -eq 0){
            dnvm-upgrade
        }
    }
}

### The main "entry point"

# Check for old DOTNET_HOME values
if($UnencodedHomes -contains $OldUserHome) {
    _WriteOut -ForegroundColor Yellow "WARNING: Found '$OldUserHome' in your $HomeEnvVar value. This folder has been deprecated."
    if($UnencodedHomes -notcontains $DefaultUserHome) {
        _WriteOut -ForegroundColor Yellow "WARNING: Didn't find '$DefaultUserHome' in your $HomeEnvVar value. You should run '$CommandName setup' to upgrade."
    }
}

# Check for old KRE_HOME variable
if(Test-Path env:\KRE_HOME) {
    _WriteOut -ForegroundColor Yellow "WARNING: Found a KRE_HOME environment variable. This variable has been deprecated and should be removed, or it may interfere with DNVM and the .NET Execution environment"
}

# Read arguments

$cmd = $args[0]

if($args.Length -gt 1) {
    $cmdargs = @($args[1..($args.Length-1)])
} else {
    $cmdargs = @()
}

# Can't add this as script-level arguments because they mask '-a' arguments in subcommands!
# So we manually parse them :)
if($cmdargs -icontains "-amd64") {
    $CompatArch = "x64"
    _WriteOut "The -amd64 switch has been deprecated. Use the '-arch x64' parameter instead"
} elseif($cmdargs -icontains "-x86") {
    $CompatArch = "x86"
    _WriteOut "The -x86 switch has been deprecated. Use the '-arch x86' parameter instead"
} elseif($cmdargs -icontains "-x64") {
    $CompatArch = "x64"
    _WriteOut "The -x64 switch has been deprecated. Use the '-arch x64' parameter instead"
}
$cmdargs = @($cmdargs | Where-Object { @("-amd64", "-x86", "-x64") -notcontains $_ })

if(!$cmd) {
    Check-Runtimes
    $cmd = "help"
    $Script:ExitCode = $ExitCodes.InvalidArguments
}

# Check for the command and run it
try {
    if(Get-Command -Name "$CommandPrefix$cmd" -ErrorAction SilentlyContinue) {
        _WriteDebug "& dnvm-$cmd $cmdargs"
        Invoke-Command ([ScriptBlock]::Create("dnvm-$cmd $cmdargs"))
    }
    else {
        _WriteOut "Unknown command: '$cmd'"
        dnvm-help
        $Script:ExitCode = $ExitCodes.UnknownCommand
    }
} catch {
    throw
    if(!$Script:ExitCode) { $Script:ExitCode = $ExitCodes.OtherError }
}

_WriteDebug "=== End $CommandName (Exit Code $Script:ExitCode) ==="
_WriteDebug ""
exit $Script:ExitCode
