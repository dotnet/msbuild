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
 
$Result = CheckRequiredVariables


if(!$Result)
{
    # fail silently if the required variables are not available for publishing the file
    exit 0
}

if(![System.IO.File]::Exists($file))
{
    throw "$file not found"
}

$fileName = [System.IO.Path]::GetFileName($file)

if([System.IO.Path]::GetExtension($file).ToLower() -eq ".zip")
{
    $Folder = "Binaries"
}
elseif([System.IO.Path]::GetExtension($file).ToLower() -eq ".msi")
{
    $Folder = "Installers"
}


Write-Host "Uploading $fileName to dotnet feed.."

$Upload_URI = "https://$env:STORAGE_ACCOUNT.blob.core.windows.net/$env:STORAGE_CONTAINER/$env:CHANNEL/$Folder/$env:DOTNET_BUILD_VERSION/$fileName$env:SASTOKEN"

Invoke-WebRequest -URI "$Upload_URI" -Method PUT -Headers @{"x-ms-blob-type"="BlockBlob"; "x-ms-date"="2015-10-23";"x-ms-version"="2013-08-15"} -InFile $file

$ReturnCode = $LASTEXITCODE

if($ReturnCode -eq 0)
{
    Write-Host "Successfully uploaded $file to dotnet feed."
}
{
    Write-Host "Failed to upload $file to dotnet feed."
}

exit $ReturnCode

