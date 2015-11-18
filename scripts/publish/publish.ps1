#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$file = $(throw "Specify the full path to the file to be uploaded")
)

function CheckRequiredVariables 
{
    if([string]::IsNullOrEmpty($env:DOTNET_BUILD_VERSION))
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
    Write-Host "Uploading $Uploadfile to dotnet feed to.."

    # use azure cli to upload to blob storage. We cannot use Invoke-WebRequest to do this becuase azure has a max limit of 64mb that can be uploaded using REST
    #$statusCode = (Invoke-WebRequest -URI "$Upload_URI" -Method PUT -Headers @{"x-ms-blob-type"="BlockBlob"; "x-ms-date"="2015-10-23";"x-ms-version"="2013-08-15"} -InFile $Uploadfile).StatusCode
    azure storage blob upload --quiet --container $env:STORAGE_CONTAINER --blob $Blob --blobtype block --connection-string "$env:CONNECTION_STRING" --file $Uploadfile

    if($LastExitCode -eq 0)
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
    $zipBlob = "$env:CHANNEL/Binaries/$env:DOTNET_BUILD_VERSION/$fileName"

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
    $indexContent = "Binaries/$env:DOTNET_BUILD_VERSION/$fileName"
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

function UploadInstallers($msiFile)
{
    $fileName = [System.IO.Path]::GetFileName($msiFile)
    $msiBlob = "$env:CHANNEL/Installers/$env:DOTNET_BUILD_VERSION/$fileName"

    if(-Not (UploadFile $msiBlob $msiFile))
    {
        return -1
    }

    Write-Host "Updating the latest dotnet installer for windows.."
    $msiBlobLatest = "$env:CHANNEL/Installers/Latest/dotnet-win-x64.latest.msi"

    if(-Not (UploadFile $msiBlobLatest $msiFile))
    {
        return -1
    }

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
elseif([System.IO.Path]::GetExtension($file).ToLower() -eq ".msi")
{
    $result = UploadInstallers $file
}

exit $result
