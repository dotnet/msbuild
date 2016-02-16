#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [Parameter(Mandatory=$true)][string]$file
)

. "$PSScriptRoot\..\common\_common.ps1"

function CheckRequiredVariables 
{
    if([string]::IsNullOrEmpty($env:DOTNET_CLI_VERSION))
    {
        return $false
    }

    # this variable is set by the CI system
    if([string]::IsNullOrEmpty($env:SASTOKEN)) 
    {
        return $false
    }

    # this variable is set by the CI system
    if([string]::IsNullOrEmpty($env:STORAGE_ACCOUNT))
    {
        return $false
    }

    # this variable is set by the CI system
    if([string]::IsNullOrEmpty($env:STORAGE_CONTAINER))
    {
        return $false
    }

    # this variable is set by the CI system
    if([string]::IsNullOrEmpty($env:CHANNEL))
    {
        return $false
    }

    # this variable is set by the CI system
    if([string]::IsNullOrEmpty($env:CONNECTION_STRING))
    {
        return $false
    }

    return $true
}

function UploadFile($Blob, $Uploadfile)
{
    Write-Host "Uploading $Uploadfile to dotnet feed."

    if([string]::IsNullOrEmpty($env:HOME))
    {
        $env:HOME=Get-Location
    }

    # use azure cli to upload to blob storage. We cannot use Invoke-WebRequest to do this becuase azure has a max limit of 64mb that can be uploaded using REST
    #$statusCode = (Invoke-WebRequest -URI "$Upload_URI" -Method PUT -Headers @{"x-ms-blob-type"="BlockBlob"; "x-ms-date"="2015-10-23";"x-ms-version"="2013-08-15"} -InFile $Uploadfile).StatusCode
    azure storage blob upload --quiet --container $env:STORAGE_CONTAINER --blob $Blob --blobtype block --connection-string "$env:CONNECTION_STRING" --file $Uploadfile | Out-Host

    if($?)
    {
        Write-Host "Successfully uploaded $Uploadfile to dotnet feed."
        return $true
    }
    else
    {
        Write-Host "Failed to upload $Uploadfile to dotnet feed."
        return $false
    }
}

function UploadBinaries($zipFile)
{
    $result = -1
    $fileName = [System.IO.Path]::GetFileName($zipFile)
    $zipBlob = "$env:CHANNEL/Binaries/$env:DOTNET_CLI_VERSION/$fileName"

    if(-Not (UploadFile $zipBlob $zipFile))
    {
        return -1
    }

    Write-Host "Updating the latest dotnet binaries for windows.."
    $zipBlobLatest = "$env:CHANNEL/Binaries/Latest/dotnet-win-x64.latest.zip"

    if(-Not (UploadFile $zipBlobLatest $zipFile))
    {
        return -1
    }


    # update the index file too
    $indexContent = "Binaries/$env:DOTNET_CLI_VERSION/$fileName"
    $indexFile = "$env:TEMP\latest.win.index"
    $indexContent | Out-File -FilePath $indexFile

    # upload the index file
    $indexBlob = "$env:CHANNEL/dnvm/latest.win.index"

    if(-Not (UploadFile $indexBlob $indexFile))
    {
        return -1
    }

    # update the version file
    $versionFile = Convert-Path $PSScriptRoot\..\..\artifacts\win7-x64\stage2\.version
    $versionBlob = "$env:CHANNEL/dnvm/latest.win.version"

    if(-Not (UploadFile $versionBlob $versionFile))
    {
        return -1
    }

    return 0
}

function UploadInstallers($installerFile)
{
    $fileName = [System.IO.Path]::GetFileName($installerFile)
    $installerBlob = "$env:CHANNEL/Installers/$env:DOTNET_CLI_VERSION/$fileName"

    if(-Not (UploadFile $installerBlob $installerFile))
    {
        return -1
    }

    Write-Host "Updating the latest dotnet installer for windows.."
    $installerBlobLatest = "$env:CHANNEL/Installers/Latest/dotnet-win-x64.latest.exe"

    if(-Not (UploadFile $installerBlobLatest $installerFile))
    {
        return -1
    }

    return 0
}

function UploadVersionBadge($badgeFile)
{
    $fileName = "windows_$Configuration_$([System.IO.Path]::GetFileName($badgeFile))"
    
    Write-Host "Uploading the version badge to Latest"
    UploadFile "$env:CHANNEL/Binaries/Latest/$fileName" $badgeFile
    
    Write-Host "Uploading the version badge to $env:DOTNET_CLI_VERSION"
    UploadFile "$env:CHANNEL/Binaries/$env:DOTNET_CLI_VERSION/$fileName" $badgeFile

    return 0
}

if(!(CheckRequiredVariables))
{
    # fail silently if the required variables are not available for publishing the file
   exit 0
}

if(![System.IO.File]::Exists($file))
{
    throw "$file not found"
}

$result = $false

if([System.IO.Path]::GetExtension($file).ToLower() -eq ".zip")
{
    $result = UploadBinaries $file
}
elseif([System.IO.Path]::GetExtension($file).ToLower() -eq ".exe")
{
    $result = UploadInstallers $file
}
elseif ([System.IO.Path]::GetExtension($file).ToLower() -eq ".svg")
{
    $result = UploadVersionBadge $file
}

exit $result
