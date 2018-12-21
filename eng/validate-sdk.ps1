Param(
  [string] $barToken,
  [string] $gitHubPat,
  [string] $configuration = "Debug"
)

$ErrorActionPreference = "Stop"
. $PSScriptRoot\common\tools.ps1
$LocalNugetConfigSourceName = "arcade-local"

function Check-ExitCode ($exitCode)
{
  if ($exitCode -ne 0) {
    Write-Host "Arcade self-build failed"
    ExitWithExitCode $exitCode
  }
}

function StopDotnetIfRunning
{
    $dotnet = Get-Process "dotnet" -ErrorAction SilentlyContinue
    if ($dotnet) {
        stop-process $dotnet
    }
}

function AddSourceToNugetConfig([string]$nugetConfigPath, [string]$source) 
{
    Write-Host "Adding '$source' to '$nugetConfigPath'..."
    
    $nugetConfig = New-Object XML
    $nugetConfig.PreserveWhitespace = $true
    $nugetConfig.Load($nugetConfigPath)
    $packageSources = $nugetConfig.SelectSingleNode("//packageSources")
    $keyAttribute = $nugetConfig.CreateAttribute("key")
    $keyAttribute.Value = $LocalNugetConfigSourceName
    $valueAttribute = $nugetConfig.CreateAttribute("value")
    $valueAttribute.Value = $source
    $newSource = $nugetConfig.CreateElement("add")
    $newSource.Attributes.Append($keyAttribute)
    $newSource.Attributes.Append($valueAttribute)
    $packageSources.AppendChild($newSource)
    $nugetConfig.Save($nugetConfigPath)
}

function MoveArtifactsToValidateSdkFolder([string]$artifactsDir, [string]$validateSdkFolderName, [string]$repoRoot)
{
    Rename-Item -Path $artifactsDir -NewName $validateSdkFolderName -Force
  
    if (!(Test-Path -Path $artifactsDir)) {
        Create-Directory $artifactsDir
    }
  
    Move-Item -Path (Join-Path $repoRoot $validateSdkFolderName) -Destination $artifactsDir -Force
}

try {
  Write-Host "STEP 1: Build and create local packages"
  
  Push-Location $PSScriptRoot
  
  $validateSdkFolderName = "validatesdk"
  $validateSdkDir = Join-Path $ArtifactsDir $validateSdkFolderName
  $packagesSource = Join-Path (Join-Path (Join-Path $validateSdkDir "packages") $configuration) "NonShipping"
  $nugetConfigPath = Join-Path $RepoRoot "NuGet.config"
  
  . .\common\build.ps1 -restore -build -pack -configuration $configuration
  
  # This is a temporary solution. When https://github.com/dotnet/arcade/issues/1293 is closed
  # we'll be able to pass a container name to build.ps1 which will put the outputs in the
  # artifacts-<container-name> folder.
  MoveArtifactsToValidateSdkFolder $ArtifactsDir $validateSdkFolderName $RepoRoot
  
  Write-Host "STEP 2: Build using the local packages"
  
  AddSourceToNugetConfig $nugetConfigPath $packagesSource
   
  Write-Host "Updating Dependencies using Darc..."

  . .\common\darc-init.ps1
  
  $DarcExe = "$env:USERPROFILE\.dotnet\tools"
  $DarcExe = Resolve-Path $DarcExe

  & $DarcExe\darc.exe update-dependencies --packages-folder $packagesSource --password $barToken --github-pat $gitHubPat --channel ".NET Tools - Latest"
  
  Check-ExitCode $lastExitCode
  StopDotnetIfRunning
  
  Write-Host "Building with updated dependencies"

  . .\common\build.ps1 -configuration $configuration @Args  /p:AdditionalRestoreSources=$packagesSource
}
catch {
  Write-Host $_
  Write-Host $_.Exception
  Write-Host $_.ScriptStackTrace
  ExitWithExitCode 1
}
finally {
  Write-Host "Cleaning up workspace..."
  StopDotnetIfRunning
  Pop-Location
  Write-Host "Finished building Arcade SDK with validation enabled!"
}
