[CmdletBinding(PositionalBinding=$false)]
Param(
  [bool] $noninteractive = $false,
  [string] $dockerImageName,
  [string] $dockerContainerTag = "dotnetcli-build",
  [string] $dockerContainerName = "dotnetcli-build-container",
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$additionalArgs
)

# sample command line:
# .\eng\dockerrun.ps1 -dockerImageName ubuntu.18.04 --test --pack --publish

Write-Host "Docker image name: $dockerImageName"
Write-Host "Additional args: $additionalArgs"

. $PSScriptRoot\common\tools.ps1

$dockerFile = Resolve-Path (Join-Path $RepoRoot "scripts\docker\$dockerImageName")

docker build --build-arg USER_ID=1000 -t "$dockerContainerTag" $dockerFile

$interactiveFlag = "-i"
if ($noninteractive)
{
  $interactiveFlag = ""
}

$joinedAdditionalArgs = $additionalArgs -Join ' '
$scriptContents = @"
. `$HOME/.bashrc
/opt/code/run-build.sh $joinedAdditionalArgs || echo failed
export PATH=/opt/code/.dotnet-${dockerImageName}:`$PATH
export ArtifactsDir=/opt/code/artifacts-$dockerImageName
export DOTNET_INSTALL_DIR=/opt/code/.dotnet-$dockerImageName
"@

$scriptContents = $scriptContents -replace "`r`n", "`n"

Set-Content -NoNewline -Path $RepoRoot\artifacts-$dockerImageName\dockerinit.sh -Value $scriptContents

docker run $interactiveFlag -t --rm --sig-proxy=true `
  --name "$dockerContainerName" `
  -v "${RepoRoot}:/opt/code" `
  -e DOTNET_CORESDK_IGNORE_TAR_EXIT_CODE=1 `
  -e CHANNEL `
  -e DOTNET_BUILD_SKIP_CROSSGEN `
  -e PUBLISH_TO_AZURE_BLOB `
  -e NUGET_FEED_URL `
  -e NUGET_API_KEY `
  -e ARTIFACT_STORAGE_ACCOUNT `
  -e ARTIFACT_STORAGE_CONTAINER `
  -e CHECKSUM_STORAGE_ACCOUNT `
  -e CHECKSUM_STORAGE_CONTAINER `
  -e BLOBFEED_STORAGE_CONTAINER `
  -e CLIBUILD_SKIP_TESTS `
  -e COMMITCOUNT `
  -e DROPSUFFIX `
  -e RELEASESUFFIX `
  -e COREFXAZURECONTAINER `
  -e AZUREACCOUNTNAME `
  -e RELEASETOOLSGITURL `
  -e CORESETUPBLOBROOTURL `
  -e PB_ASSETROOTURL `
  -e PB_PACKAGEVERSIONPROPSURL `
  -e PB_PUBLISHBLOBFEEDURL `
  -e EXTERNALRESTORESOURCES `
  -e ARCADE_CONTAINER="${dockerImageName}" `
  $dockerContainerTag `
  bash --init-file /opt/code/artifacts-$dockerImageName/dockerinit.sh
