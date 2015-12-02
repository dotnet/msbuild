. "$PSScriptRoot\..\..\scripts\_common.ps1"

$IntermediatePackagesDir = "$RepoRoot\artifacts\packages\intermediate"
$PackagesDir = "$RepoRoot\artifacts\packages"

New-Item -ItemType Directory -Force -Path $IntermediatePackagesDir

$Projects = @(
    "Microsoft.DotNet.Cli.Utils",
    "Microsoft.DotNet.ProjectModel",
    "Microsoft.DotNet.ProjectModel.Workspaces",
    "Microsoft.DotNet.Runtime",
    "Microsoft.Extensions.Testing.Abstractions"
)

foreach ($ProjectName in $Projects) {
    $ProjectFile = "$RepoRoot\src\$ProjectName\project.json"
    & dotnet restore "$ProjectFile"
    if (!$?) {
        Write-Host "dotnet restore failed for: $ProjectFile"
        Exit 1
    }
    & dotnet pack "$ProjectFile" --output "$IntermediatePackagesDir"
    if (!$?) {
        Write-Host "dotnet pack failed for: $ProjectFile"
        Exit 1
    }
}

Get-ChildItem $IntermediatePackagesDir -Filter *.nupkg | Copy-Item -Destination $PackagesDir
