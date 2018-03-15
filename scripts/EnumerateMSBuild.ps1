# Write output to console and save log to send.
Start-Transcript -Path msbuild_versions.txt

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
Write-Host "Using vswhere from " + $vswhere

$vsInstances = & $vswhere -prerelease -all -format json | ConvertFrom-Json

foreach ($instance in $vsInstances)
{
    $instancePath = $instance.installationPath
    Write-Host "********************"
    write-host "Found VS: " + $instance.installationName
    
    # Look at each dll/exe in the MSBuild bin folder and get their ProductVersion
    Get-ChildItem -File -Recurse -Include ('*.dll', '*.exe') -Path "$instancePath\MSBuild\15.0\Bin" | % VersionInfo | Select-Object ProductVersion, FileName
    Write-Host "********************"
    Write-Host
}

# Check in Program Files (x86)\MSBuild for old versions.
Write-Host
Write-Host "********************"
$legacyPath = ${env:ProgramFiles(x86)} + "\MSBuild\"
Write-Host "Looking for legacy MSBuild versions: $legacyPath"
Get-ChildItem -File -Path "$legacyPath" -Recurse "Microsoft.Build*.dll" | % VersionInfo | Select-Object ProductVersion, FileName
Write-Host "********************"

# Check in the .NET 4.5+ GAC
Write-Host
Write-Host "********************"
$gacPath = ${env:windir} + "\Microsoft.NET\assembly"
Write-Host "Looking for MSBuild in the GAC: $gacPath"
Get-ChildItem -File -Path "$gacPath" -Recurse "Microsoft.Build*.dll" -Exclude "*.ni.dll" | % VersionInfo | Select-Object ProductVersion, FileName
Write-Host "********************"

# Just for completeness look in c:\Windows\assembly as well.
Write-Host
Write-Host "********************"
$gacPath = ${env:windir} + "\assembly"
Write-Host "Looking for MSBuild in the GAC: $gacPath"
Get-ChildItem -File -Path "$gacPath" -Recurse "Microsoft.Build*.dll" -Exclude "*.ni.dll" | % VersionInfo | Select-Object ProductVersion, FileName
Write-Host "********************"

Stop-Transcript