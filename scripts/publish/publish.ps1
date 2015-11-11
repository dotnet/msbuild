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

    return $true
}

function UploadFile($Upload_URI, $Uploadfile)
{
    Write-Host "Uploading $Uploadfile to dotnet feed to.."

    $statusCode = (Invoke-WebRequest -URI "$Upload_URI" -Method PUT -Headers @{"x-ms-blob-type"="BlockBlob"; "x-ms-date"="2015-10-23";"x-ms-version"="2013-08-15"} -InFile $Uploadfile).StatusCode

    if($statusCode -eq 201)
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
    $Upload_URI = "https://$env:STORAGE_ACCOUNT.blob.core.windows.net/$env:STORAGE_CONTAINER/$env:CHANNEL/Binaries/$env:DOTNET_BUILD_VERSION/$fileName$env:SASTOKEN"

    if(-Not (UploadFile $Upload_URI $zipFile))
    {
        return -1
    }

    Write-Host "Updating the latest dotnet binaries for windows.."
    $Upload_URI_Latest = "https://$env:STORAGE_ACCOUNT.blob.core.windows.net/$env:STORAGE_CONTAINER/$env:CHANNEL/Binaries/Latest/dotnet-win-x64.latest.zip$env:SASTOKEN"

    if(-Not (UploadFile $Upload_URI_Latest $zipFile))
    {
        return -1
    }


    # update the index file too
    $indexContent = "Binaries/$env:DOTNET_BUILD_VERSION/$fileName"
    $indexFile = "$env:TEMP\latest.win.index"
    $indexContent | Out-File -FilePath $indexFile

    # upload the index file
    $Upload_URI = "https://$env:STORAGE_ACCOUNT.blob.core.windows.net/$env:STORAGE_CONTAINER/$env:CHANNEL/dnvm/latest.win.index$env:SASTOKEN"

    if(-Not (UploadFile $Upload_URI $indexFile))
    {
        return -1
    }

    # update the version file
    $versionFile = Convert-Path $PSScriptRoot\..\..\artifacts\win7-x64\stage2\.version
    $Version_URI = "https://$env:STORAGE_ACCOUNT.blob.core.windows.net/$env:STORAGE_CONTAINER/$env:CHANNEL/dnvm/latest.win.version$env:SASTOKEN"

    if(-Not (UploadFile $Version_URI $versionFile))
    {
        return -1
    }

    return 0
}

function UploadInstallers($msiFile)
{
    $fileName = [System.IO.Path]::GetFileName($msiFile)
    $Upload_URI = "https://$env:STORAGE_ACCOUNT.blob.core.windows.net/$env:STORAGE_CONTAINER/$env:CHANNEL/Installers/$env:DOTNET_BUILD_VERSION/$fileName$env:SASTOKEN"

    if(-Not (UploadFile $Upload_URI $msiFile))
    {
        return -1
    }

    Write-Host "Updating the latest dotnet installer for windows.."
    $Upload_URI_Latest = "https://$env:STORAGE_ACCOUNT.blob.core.windows.net/$env:STORAGE_CONTAINER/$env:CHANNEL/Installers/Latest/dotnet-win-x64.latest.msi$env:SASTOKEN"

    if(-Not (UploadFile $Upload_URI_Latest $msiFile))
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
