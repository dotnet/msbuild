#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# MSI versioning
# Encode the CLI version to fit into the MSI versioning scheme - https://msdn.microsoft.com/en-us/library/windows/desktop/aa370859(v=vs.85).aspx
# MSI versions are 3 part
#                           major.minor.build
# Size(bits) of each part     8     8    16
# So we have 32 bits to encode the CLI version
# Starting with most significant bit this how the CLI version is going to be encoded as MSI Version
# CLI major  -> 6 bits
# CLI minor  -> 6 bits
# CLI patch  -> 6 bits
# CLI commitcount -> 14 bits
function GetMSIVersionFromCLIVersion([uint32]$Major, [uint32]$Minor, [uint32]$Patch, [uint32]$CommitCount)
{
    if($Major -ge 0x40)
    {
        throw [System.NotSupportedException] "Invalid Major version - $Major. Major version must be less than 64."
    }

    if($Minor -ge 0x40)
    {
        throw [System.NotSupportedException] "Invalid Minor version - $Minor. Minor version must be less than 64."
    }

    if($Patch -ge 0x40)
    {
        throw [System.NotSupportedException] "Invalid Patch version - $Patch. Patch version must be less than 64."
    }

    if($CommitCount -ge 0x4000)
    {
        throw [System.NotSupportedException] "Invalid CommitCount version - $CommitCount. CommitCount version must be less than 16384."
    }

    $Major = ($Major -shl 26)
    $Minor = ($Minor -shl 20)
    $Patch = ($Patch -shl 14)
    [System.UInt32]$MSIVersionNumber = ($Major -bor $Minor -bor $Patch -bor $CommitCount)

    $MSIMajor = ($MSIVersionNumber -shr 24) -band 0xFF
    $MSIMinor = ($MSIVersionNumber -shr 16) -band 0xFF
    $MSIBuild = $MSIVersionNumber -band 0xFFFF
    $MSIVersion = "$MSIMajor.$MSIMinor.$MSIBuild"

    return $MSIVersion
}

$env:ReleaseSuffix = "beta"
$env:MajorVersion = 1
$env:MinorVersion = 0
$env:PatchVersion = 0

$CommitCount = [int32](git rev-list --count HEAD)
$env:CommitCountVersion = ([string]$CommitCount).PadLeft(6, "0")

# Zero Padded Suffix for use with Nuget
$env:VersionSuffix = "$env:ReleaseSuffix-$env:CommitCountVersion"

$env:DOTNET_CLI_VERSION = "$env:MajorVersion.$env:MinorVersion.$env:PatchVersion.$env:CommitCountVersion"
$env:DOTNET_MSI_VERSION = GetMSIVersionFromCLIVersion $env:MajorVersion $env:MinorVersion $env:PatchVersion $CommitCount
