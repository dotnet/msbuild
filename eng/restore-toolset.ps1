. $PSScriptRoot\common\tools.ps1

function InstallGlobalTool ($dotnetRoot, $toolName, $toolPath, $version) {
  $dotnet = "$dotnetRoot\dotnet.exe"

  if (-not $version) {
    Write-Host "'$dotnet' tool install $toolName -v $verbosity --tool-path '$toolPath'"
    & "$dotnet" tool install $toolName --prerelease -v $verbosity --tool-path "$toolPath"
  } else {
    Write-Host "'$dotnet' tool install $toolName --version $version -v $verbosity --tool-path '$toolPath'"
    & "$dotnet" tool install $toolName --prerelease --version $version -v $verbosity --tool-path "$toolPath"
  }
}

$dotnetRoot = InitializeDotNetCli -install:$true
$Env:DOTNET_ROOT = $dotnetRoot
$repoRoot = Join-Path $PSScriptRoot ".."
$toolsDir = Join-Path $repoRoot ".tools"
$dotnetCoverageDir = Join-Path $toolsDir "dotnet-coverage"

if (!(Test-Path -Path $dotnetCoverageDir))
{
  InstallGlobalTool $dotnetRoot dotnet-coverage $dotnetCoverageDir
}
