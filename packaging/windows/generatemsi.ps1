# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$inputDir
)

. "$PSScriptRoot\..\..\scripts\common\_common.ps1"

$DotnetMSIOutput = ""
$DotnetBundleOutput = ""
$WixRoot = ""
$InstallFileswsx = "install-files.wxs"
$InstallFilesWixobj = "install-files.wixobj"

function AcquireWixTools
{
    $result = Join-Path $OutputDir WiXTools

    if(Test-Path "$result\candle.exe")
    {
        return $result
    }

    Write-Host Downloading Wixtools..
    New-Item $result -type directory -force | Out-Null
    # Download Wix version 3.10.2 - https://wix.codeplex.com/releases/view/619491
    Invoke-WebRequest -Uri https://wix.codeplex.com/downloads/get/1540241 -Method Get -OutFile $result\WixTools.zip

    Write-Host Extracting Wixtools..
    [System.IO.Compression.ZipFile]::ExtractToDirectory("$result\WixTools.zip", $result)

    if($LastExitCode -ne 0)
    {
        throw "Unable to download and extract the WixTools."
    }

    return $result
}

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
    $AuthWsxRoot =  Join-Path $RepoRoot "packaging\windows"

    .\candle.exe -nologo `
        -dDotnetSrc="$inputDir" `
        -dMicrosoftEula="$RepoRoot\packaging\osx\resources\en.lproj\eula.rtf" `
        -dBuildVersion="$env:DOTNET_MSI_VERSION" `
        -dDisplayVersion="$env:DOTNET_CLI_VERSION" `
        -dReleaseSuffix="$env:ReleaseSuffix" `
        -arch "$env:ARCHITECTURE" `
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
    $AuthWsxRoot =  Join-Path $RepoRoot "packaging\windows"

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

function RunCandleForBundle
{
    $result = $true
    pushd "$WixRoot"

    Write-Host Running candle for bundle..
    $AuthWsxRoot =  Join-Path $RepoRoot "packaging\windows"

    .\candle.exe -nologo `
        -dDotnetSrc="$inputDir" `
        -dMicrosoftEula="$RepoRoot\packaging\osx\resources\en.lproj\eula.rtf" `
        -dBuildVersion="$env:DOTNET_MSI_VERSION" `
        -dDisplayVersion="$env:DOTNET_CLI_VERSION" `
        -dReleaseSuffix="$env:ReleaseSuffix" `
        -dMsiSourcePath="$DotnetMSIOutput" `
        -arch "$env:ARCHITECTURE" `
        -ext WixBalExtension.dll `
        -ext WixUtilExtension.dll `
        -ext WixTagExtension.dll `
        "$AuthWsxRoot\bundle.wxs" | Out-Host

    if($LastExitCode -ne 0)
    {
        $result = $false
        Write-Host "Candle failed with exit code $LastExitCode."
    }

    popd
    return $result
}

function RunLightForBundle
{
    $result = $true
    pushd "$WixRoot"

    Write-Host Running light for bundle..
    $AuthWsxRoot =  Join-Path $RepoRoot "packaging\windows"

    .\light.exe -nologo `
        -cultures:en-us `
        bundle.wixobj `
        -ext WixBalExtension.dll `
        -ext WixUtilExtension.dll `
        -ext WixTagExtension.dll `
        -b "$AuthWsxRoot" `
        -out $DotnetBundleOutput | Out-Host

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

if(!(Test-Path $PackageDir))
{
    mkdir $PackageDir | Out-Null
}

$DotnetMSIOutput = Join-Path $PackageDir "dotnet-win-$env:ARCHITECTURE.$env:DOTNET_CLI_VERSION.msi"
$DotnetBundleOutput = Join-Path $PackageDir "dotnet-win-$env:ARCHITECTURE.$env:DOTNET_CLI_VERSION.exe"

Write-Host "Creating dotnet MSI at $DotnetMSIOutput"
Write-Host "Creating dotnet Bundle at $DotnetBundleOutput"

$WixRoot = AcquireWixTools


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

if(-Not (RunCandleForBundle))
{
    Exit -1
}

if(-Not (RunLight))
{
    Exit -1
}

if(-Not (RunLightForBundle))
{
    Exit -1
}

if(!(Test-Path $DotnetMSIOutput))
{
    throw "Unable to create the dotnet msi."
    Exit -1
}

if(!(Test-Path $DotnetBundleOutput))
{
    throw "Unable to create the dotnet bundle."
    Exit -1
}

Write-Host -ForegroundColor Green "Successfully created dotnet MSI - $DotnetMSIOutput"
Write-Host -ForegroundColor Green "Successfully created dotnet bundle - $DotnetBundleOutput"

_ $RepoRoot\test\Installer\testmsi.ps1 @("$DotnetMSIOutput")

$PublishScript = Join-Path $PSScriptRoot "..\..\scripts\publish\publish.ps1"
& $PublishScript -file $DotnetBundleOutput

exit $LastExitCode
