$Rid = "win7-x64"
$RepoRoot = Convert-Path (Split-Path -Parent $PSScriptRoot)
$Stage2Dir = Join-Path $RepoRoot "artifacts\$RID\stage2"
$PackageDir = Join-Path $RepoRoot "artifacts\packages\dnvm"

if(!(Test-Path $PackageDir)) {
    mkdir $PackageDir | Out-Null
}

if(![string]::IsNullOrEmpty($env:DOTNET_BUILD_VERSION)) {
    $PackageVersion = $env:DOTNET_BUILD_VERSION
} else {
    $Timestamp = [DateTime]::Now.ToString("yyyyMMddHHmmss")
    $PackageVersion = "0.0.1-alpha-t$Timestamp"
}

# Stamp the output with the commit metadata and version number
$Commit = git rev-parse HEAD

@"
$Commit
$PackageVersion
"@.Trim() > "$Stage2Dir/.version"

$PackageName = Join-Path $PackageDir "dotnet-win-x64.$PackageVersion.zip"

if (Test-Path $PackageName)
{
    del $PackageName
}

Add-Type -Assembly System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($Stage2Dir, $PackageName, "Optimal", $false)

Write-Host "Packaged stage2 to $PackageName"

$PublishScript = Join-Path $PSScriptRoot "publish.ps1"
& $PublishScript -file $PackageName

exit $LastExitCode
