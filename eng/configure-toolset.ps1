if ($env:_BuildToolPath -or $env:_InitializeToolset -or $fromVMR -or
    (Test-Path variable:global:_BuildTool) -or (Test-Path variable:global:_InitializeToolset) -or
    ((Test-Path variable:help) -and $help) -or ((Test-Path variable:clean) -and $clean) -or
    ((Test-Path variable:properties) -and $properties -and
      ($properties.Contains('/help') -or $properties.Contains('/?')))) {
  return
}

# Keep both SDKs in the repo-local installation unless the caller supplies one.
$useInstalledDotNetCli = $false
$engineeringDotNetRoot = InitializeDotNetCli -install:$restore
if ($restore) {
  InstallDotNet -dotnetRoot $engineeringDotNetRoot -version $GlobalJson.sdk.version -skipNonVersionedFiles $true -runtimeSourceFeed $runtimeSourceFeed -runtimeSourceFeedKey $runtimeSourceFeedKey
}

$env:DOTNET_ROOT = $engineeringDotNetRoot
$env:DOTNET_HOST_PATH = Join-Path $engineeringDotNetRoot 'dotnet.exe'
$engineeringEngine = if ($msbuildEngine) { $msbuildEngine } else { GetDefaultMSBuildEngine }
if ($engineeringEngine -eq 'dotnet') {
  $engineeringMSBuild = Join-Path $engineeringDotNetRoot "sdk\$($GlobalJson.tools.dotnet)\MSBuild.dll"
  $global:_BuildTool = @{
    Path = $env:DOTNET_HOST_PATH
    Command = '"' + $engineeringMSBuild + '"'
  }
}
