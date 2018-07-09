# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$inputDir,
    [Parameter(Mandatory=$true)][string]$MSBuildExtensionsMSIOutput,
    [Parameter(Mandatory=$true)][string]$WixRoot,
    [Parameter(Mandatory=$true)][string]$ProductMoniker,
    [Parameter(Mandatory=$true)][string]$DotnetMSIVersion,
    [Parameter(Mandatory=$true)][string]$DotnetCLIDisplayVersion,
    [Parameter(Mandatory=$true)][string]$DotnetCLINugetVersion,
    [Parameter(Mandatory=$true)][string]$UpgradeCode,
    [Parameter(Mandatory=$true)][string]$Architecture
)

. "$PSScriptRoot\..\..\..\scripts\common\_common.ps1"
$RepoRoot = Convert-Path "$PSScriptRoot\..\..\.."

$InstallFileswsx = "install-msbuild-extensions-files.wxs"
$InstallFilesWixobj = "install-msbuild-extensions-files.wixobj"

function RunHeat
{
    $result = $true
    pushd "$WixRoot"

    Write-Output Running heat..

    .\heat.exe dir `"$inputDir`" -template fragment -sreg -gg -var var.DotnetSrc -cg InstallFiles -srd -dr MSBUILDEXTENSIONSHOME -out $InstallFileswsx | Out-Host

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Output "Heat failed with exit code $LastExitCode."
    }

    popd
    return $result
}

function RunCandle
{
    $result = $true
    pushd "$WixRoot"

    Write-Output Running candle..
    $AuthWsxRoot =  Join-Path $RepoRoot "packaging\windows\msbuildextensions"

    .\candle.exe -nologo `
        -dDotnetSrc="$inputDir" `
        -dMicrosoftEula="$RepoRoot\packaging\windows\clisdk\dummyeula.rtf" `
        -dProductMoniker="$ProductMoniker" `
        -dBuildVersion="$DotnetMSIVersion" `
        -dDisplayVersion="$DotnetCLIDisplayVersion" `
        -dNugetVersion="$DotnetCLINugetVersion" `
        -dUpgradeCode="$UpgradeCode" `
        -arch "$Architecture" `
        -ext WixDependencyExtension.dll `
        "$AuthWsxRoot\msbuildextensions.wxs" `
        "$AuthWsxRoot\provider.wxs" `
        "$AuthWsxRoot\registrykeys.wxs" `
        $InstallFileswsx | Out-Host

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Output "Candle failed with exit code $LastExitCode."
    }

    popd
    return $result
}

function RunLight
{
    $result = $true
    pushd "$WixRoot"

    Write-Output Running light..
    $CabCache = Join-Path $WixRoot "cabcache"
    $AuthWsxRoot =  Join-Path $RepoRoot "packaging\windows\msbuildextensions"

    .\light.exe -nologo -ext WixUIExtension -ext WixDependencyExtension -ext WixUtilExtension `
        -cultures:en-us `
        msbuildextensions.wixobj `
        provider.wixobj `
        registrykeys.wixobj `
        $InstallFilesWixobj `
        -b "$inputDir" `
        -b "$AuthWsxRoot" `
        -reusecab `
        -cc "$CabCache" `
        -out $MSBuildExtensionsMSIOutput | Out-Host

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Output "Light failed with exit code $LastExitCode."
    }

    popd
    return $result
}

if(!(Test-Path $inputDir))
{
    throw "$inputDir not found"
}

Write-Output "Creating MSBuild Extensions MSI at $MSBuildExtensionsMSIOutput"

if([string]::IsNullOrEmpty($WixRoot))
{
    Exit -1
}

if(-Not (RunHeat))
{
    Exit -1
}

if(-Not (RunCandle))
{
    Exit -1
}

if(-Not (RunLight))
{
    Exit -1
}

if(!(Test-Path $MSBuildExtensionsMSIOutput))
{
    throw "Unable to create the MSBuild Extensions msi."
    Exit -1
}

Write-Output -ForegroundColor Green "Successfully created MSBuild Extensions MSI - $MSBuildExtensionsMSIOutput"

exit $LastExitCode
