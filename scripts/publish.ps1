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

    if(UploadFile $Upload_URI $zipFile)
    {
        # update the index file too
        $indexContent = "Binaries/$env:DOTNET_BUILD_VERSION/$fileName"
        $indexFile = "$env:TEMP\latest.win.index"
        $indexContent | Out-File -FilePath $indexFile

        # upload the index file
        $Upload_URI = "https://$env:STORAGE_ACCOUNT.blob.core.windows.net/$env:STORAGE_CONTAINER/$env:CHANNEL/dnvm/latest.win.index$env:SASTOKEN"

        if(UploadFile $Upload_URI $indexFile)
        {
            $result = 0
        }
    }

    return $result
}

function UploadInstallers($msiFile)
{
    $result = -1
    $fileName = [System.IO.Path]::GetFileName($msiFile)
    $Upload_URI = "https://$env:STORAGE_ACCOUNT.blob.core.windows.net/$env:STORAGE_CONTAINER/$env:CHANNEL/Installers/$env:DOTNET_BUILD_VERSION/$fileName$env:SASTOKEN"

    if(UploadFile $Upload_URI $msiFile)
    {
        $result = 0
    }

    return $result
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
