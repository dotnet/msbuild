Push-Location $PSScriptRoot
[Environment]::CurrentDirectory = $PWD

$ProjectsDir = "..\..\src"
$IntermediatePackagesDir = "..\..\artifacts\packages\intermediate"
$PackagesDir = "..\..\artifacts\packages"

New-Item -ItemType Directory -Force -Path $IntermediatePackagesDir

foreach ($file in [System.IO.Directory]::EnumerateFiles($ProjectsDir, "project.json", "AllDirectories")) {
    & dotnet restore "$file"
    if (!$?) {
        Write-Host "dotnet restore failed for: $file"
        Exit 1
    }
    & dotnet pack "$file" --output "$IntermediatePackagesDir"
    if (!$?) {
        Write-Host "dotnet pack failed for: $file"
        Exit 1
    }
}

Get-ChildItem $IntermediatePackagesDir -Filter *.nupkg | Copy-Item -Destination $PackagesDir

Pop-Location
[Environment]::CurrentDirectory = $PWD
