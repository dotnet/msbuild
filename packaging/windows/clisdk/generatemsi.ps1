# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$inputDir,
    [Parameter(Mandatory=$true)][string]$DotnetMSIOutput,
    [Parameter(Mandatory=$true)][string]$WixRoot,
    [Parameter(Mandatory=$true)][string]$DotnetMSIVersion,
    [Parameter(Mandatory=$true)][string]$DotnetCLIVersion,
    [Parameter(Mandatory=$true)][string]$Architecture,
    [Parameter(Mandatory=$true)][string]$ReleaseSuffix
)

. "$PSScriptRoot\..\..\..\scripts\common\_common.ps1"
$RepoRoot = Convert-Path "$PSScriptRoot\..\..\.."

$InstallFileswsx = "install-files.wxs"
$InstallFilesWixobj = "install-files.wixobj"

function RunHeat
{
    $result = $true
    pushd "$WixRoot"

    Write-Host Running heat..

    .\heat.exe dir `"$inputDir`" -template fragment -sreg -gg -var var.DotnetSrc -cg InstallFiles -srd -dr DOTNETHOME -out $InstallFileswsx | Out-Host

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Host "Heat failed with exit code $LastExitCode."
    }

    popd
    return $result
}

function RunCandle
{
    $result = $true
    pushd "$WixRoot"

    Write-Host Running candle..
    $AuthWsxRoot =  Join-Path $RepoRoot "packaging\windows\clisdk"

    .\candle.exe -nologo `
        -dDotnetSrc="$inputDir" `
        -dMicrosoftEula="$RepoRoot\packaging\osx\resources\en.lproj\eula.rtf" `
        -dBuildVersion="$DotnetMSIVersion" `
        -dDisplayVersion="$DotnetCLIVersion" `
        -dReleaseSuffix="$ReleaseSuffix" `
        -arch "$Architecture" `
        -ext WixDependencyExtension.dll `
        "$AuthWsxRoot\dotnet.wxs" `
        "$AuthWsxRoot\provider.wxs" `
        "$AuthWsxRoot\registrykeys.wxs" `
        "$AuthWsxRoot\checkbuildtype.wxs" `
        $InstallFileswsx | Out-Host

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Host "Candle failed with exit code $LastExitCode."
    }

    popd
    return $result
}

function RunLight
{
    $result = $true
    pushd "$WixRoot"

    Write-Host Running light..
    $CabCache = Join-Path $WixRoot "cabcache"
    $AuthWsxRoot =  Join-Path $RepoRoot "packaging\windows\clisdk"

    .\light.exe -nologo -ext WixUIExtension -ext WixDependencyExtension -ext WixUtilExtension `
        -cultures:en-us `
        dotnet.wixobj `
        provider.wixobj `
        registrykeys.wixobj `
        checkbuildtype.wixobj `
        $InstallFilesWixobj `
        -b "$inputDir" `
        -b "$AuthWsxRoot" `
        -reusecab `
        -cc "$CabCache" `
        -out $DotnetMSIOutput | Out-Host

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Host "Light failed with exit code $LastExitCode."
    }

    popd
    return $result
}

if(!(Test-Path $inputDir))
{
    throw "$inputDir not found"
}

Write-Host "Creating dotnet MSI at $DotnetMSIOutput"

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

if(!(Test-Path $DotnetMSIOutput))
{
    throw "Unable to create the dotnet msi."
    Exit -1
}

Write-Host -ForegroundColor Green "Successfully created dotnet MSI - $DotnetMSIOutput"

exit $LastExitCode
