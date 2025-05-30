param (
    [Switch]$skipVcpkg
)

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

if (!($skipVcpkg))
{
  $artifactsIntermediateDir = Join-Path $repoRoot (Join-Path "artifacts" "Intermediate")
  if (!(Test-Path -Path $artifactsIntermediateDir))
  {
    New-Item -ItemType Directory -Force -Path $artifactsIntermediateDir
  }

  $vcpkgDir = Join-Path $artifactsIntermediateDir "vcpkg"
  if (Test-Path -Path $vcpkgDir) {
    cd $vcpkgDir
    git pull
    ./vcpkg upgrade
  } else {
    cd $artifactsIntermediateDir
    $env:GIT_REDIRECT_STDERR="2>&1"
    git clone https://github.com/Microsoft/vcpkg.git
    cd $vcpkgDir
    ./bootstrap-vcpkg.bat
    ./vcpkg integrate install
    ./vcpkg install zstd:x86-windows-static
    ./vcpkg install zstd:x64-windows-static
    ./vcpkg install zstd:arm64-windows-static
  }
}
