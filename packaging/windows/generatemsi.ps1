# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [Parameter(Mandatory=$true)][string]$inputDir
)

. "$PSScriptRoot\..\..\scripts\common\_common.ps1"

$DotnetMSIOutput = ""
$WixRoot = ""
$InstallFileswsx = "install-files.wxs"
$InstallFilesWixobj = "install-files.wixobj"

function AcquireWixTools
{
    pushd "$Stage2Dir\bin"

    Write-Host Restoring Wixtools..

    $result = $env:TEMP
    
    .\dotnet restore $RepoRoot\packaging\windows\WiXTools --packages $result | Out-Null

    if($LastExitCode -ne 0)
    {
        $result = ""
        Write-Host "dotnet restore failed with exit code $LastExitCode."
    }
    else
    {
        $result = Join-Path $result WiX\3.10.0.2103-pre1\tools
    }

    popd    
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

    .\candle.exe -dDotnetSrc="$inputDir" -dMicrosoftEula="$RepoRoot\packaging\osx\resources\en.lproj\eula.rtf" -dBuildVersion="$env:DOTNET_CLI_VERSION" -arch x64 `
        -ext WixDependencyExtension.dll `
        "$AuthWsxRoot\dotnet.wxs" `
        "$AuthWsxRoot\provider.wxs" `
        "$AuthWsxRoot\registrykeys.wxs" `
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

    .\light -ext WixUIExtension -ext WixDependencyExtension -ext WixUtilExtension `
        -cultures:en-us `
        dotnet.wixobj `
        provider.wixobj `
        registrykeys.wixobj `
        $InstallFilesWixobj `
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

if(!(Test-Path $PackageDir)) 
{
    mkdir $PackageDir | Out-Null
}

$DotnetMSIOutput = Join-Path $PackageDir "dotnet-win-x64.$env:DOTNET_CLI_VERSION.msi"

Write-Host "Creating dotnet MSI at $DotnetMSIOutput"

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

_ $PSScriptRoot\testmsi.ps1 @("$DotnetMSIOutput")

$PublishScript = Join-Path $PSScriptRoot "..\..\scripts\publish\publish.ps1"
& $PublishScript -file $DotnetMSIOutput

exit $LastExitCode
