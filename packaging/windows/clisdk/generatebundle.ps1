# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$CLISDKMSIFile,
    [Parameter(Mandatory=$true)][string]$ASPNETRuntimeWixLibFile,
    [Parameter(Mandatory=$true)][string]$SharedFxMSIFile,
    [Parameter(Mandatory=$true)][string]$HostFxrMSIFile,
    [Parameter(Mandatory=$true)][string]$SharedHostMSIFile,
    [Parameter(Mandatory=$true)][string]$DotnetBundleOutput,
    [Parameter(Mandatory=$true)][string]$WixRoot,
    [Parameter(Mandatory=$true)][string]$ProductMoniker,
    [Parameter(Mandatory=$true)][string]$DotnetMSIVersion,
    [Parameter(Mandatory=$true)][string]$DotnetCLIDisplayVersion,
    [Parameter(Mandatory=$true)][string]$DotnetCLINugetVersion,
    [Parameter(Mandatory=$true)][string]$UpgradeCode,
    [Parameter(Mandatory=$true)][string]$Architecture,
    [Parameter(Mandatory=$true)][string]$DotNetRuntimeVersion,
    [Parameter(Mandatory=$true)][string]$AspNetCoreVersion
)

. "$PSScriptRoot\..\..\..\scripts\common\_common.ps1"
$RepoRoot = Convert-Path "$PSScriptRoot\..\..\.."

function RunCandleForBundle
{
    $result = $true
    pushd "$WixRoot"

    Write-Output Running candle for bundle..
    $AuthWsxRoot =  Join-Path $RepoRoot "packaging\windows\clisdk"

    .\candle.exe -nologo `
        -dDotnetSrc="$inputDir" `
        -dMicrosoftEula="$RepoRoot\packaging\windows\clisdk\dummyeula.rtf" `
        -dProductMoniker="$ProductMoniker" `
        -dBuildVersion="$DotnetMSIVersion" `
        -dDisplayVersion="$DotnetCLIDisplayVersion" `
        -dNugetVersion="$DotnetCLINugetVersion" `
        -dCLISDKMsiSourcePath="$CLISDKMSIFile" `
        -dUpgradeCode="$UpgradeCode" `
        -dSharedFXMsiSourcePath="$SharedFxMSIFile" `
        -dHostFXRMsiSourcePath="$HostFxrMSIFile" `
        -dSharedHostMsiSourcePath="$SharedHostMSIFile" `
        -dAdditionalSharedFXMsiSourcePath="$AdditionalSharedFxMSIFile" `
        -dAdditionalHostFXRMsiSourcePath="$AdditionalHostFxrMSIFile" `
        -dAdditionalSharedHostMsiSourcePath="$AdditionalSharedHostMSIFile" `
        -dDotNetRuntimeVersion="$DotNetRuntimeVersion" `
        -dAspNetCoreVersion="$AspNetCoreVersion" `
        -arch "$Architecture" `
        -ext WixBalExtension.dll `
        -ext WixUtilExtension.dll `
        -ext WixTagExtension.dll `
        "$AuthWsxRoot\bundle.wxs" | Out-Host

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Output "Candle failed with exit code $LastExitCode."
    }

    popd
    return $result
}

function RunLightForBundle
{
    $result = $true
    pushd "$WixRoot"

    Write-Output Running light for bundle..
    $AuthWsxRoot =  Join-Path $RepoRoot "packaging\windows\clisdk"

    .\light.exe -nologo `
        -cultures:en-us `
        bundle.wixobj `
        $ASPNETRuntimeWixlibFile `
        -ext WixBalExtension.dll `
        -ext WixUtilExtension.dll `
        -ext WixTagExtension.dll `
        -b "$AuthWsxRoot" `
        -out $DotnetBundleOutput | Out-Host

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Output "Light failed with exit code $LastExitCode."
    }

    popd
    return $result
}


if(!(Test-Path $CLISDKMSIFile))
{
    throw "$CLISDKMSIFile not found"
}

if(!(Test-Path $ASPNETRuntimeWixLibFile))
{
    throw "$ASPNETRuntimeWixLibFile not found"
}

Write-Output "Creating dotnet Bundle at $DotnetBundleOutput"

if([string]::IsNullOrEmpty($WixRoot))
{
    Exit -1
}

if(-Not (RunCandleForBundle))
{
    Exit -1
}

if(-Not (RunLightForBundle))
{
    Exit -1
}

if(!(Test-Path $DotnetBundleOutput))
{
    throw "Unable to create the dotnet bundle."
    Exit -1
}

Write-Output -ForegroundColor Green "Successfully created dotnet bundle - $DotnetBundleOutput"

exit $LastExitCode
