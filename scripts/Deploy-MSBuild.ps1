[CmdletBinding(PositionalBinding=$false)]
Param(
  [Parameter(Mandatory = $true)]
  [string] $destination,
  [ValidateSet('Debug','Release')]
  [string] $configuration = "Debug"
)

function Copy-WithBackup ($origin) {
    $destinationPath = Join-Path -Path $destination -ChildPath (Split-Path $origin -leaf)

    if (Test-Path $destinationPath -PathType Leaf) {
        # Back up previous copy of the file
        Copy-Item $destinationPath $BackupFolder -ErrorAction Stop
    }

    Copy-Item $origin $destinationPath -ErrorAction Stop
}

# TODO: find destination in PATH if not specified

# TODO: identify processes likely to be using target MSBuild and warn/offer to kill

# TODO: find most-recently-built MSBuild and make it default $configuration

$BackupFolder = New-Item (Join-Path $destination -ChildPath "Backup-$(Get-Date -Format FileDateTime)") -itemType directory -ErrorAction Stop

Write-Verbose "Copying $configuration MSBuild to $destination"
Write-Host "Existing MSBuild assemblies backed up to $BackupFolder"

$filesToCopyToBin = @(
    "artifacts\bin\MSBuild.Bootstrap\$configuration\net472\MSBuild.exe"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\net472\Microsoft.Build.dll"
    "artifacts\bin\Microsoft.Build.Conversion\$configuration\net472\Microsoft.Build.Conversion.Core.dll"
    "artifacts\bin\Microsoft.Build.Engine\$configuration\net472\Microsoft.Build.Engine.dll"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\net472\Microsoft.Build.Framework.dll"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\net472\Microsoft.Build.Tasks.Core.dll"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\net472\Microsoft.Build.Utilities.Core.dll"
    "artifacts\bin\MSBuildTaskHost\$configuration\net35\MSBuildTaskHost.exe"
    "artifacts\bin\MSBuildTaskHost\$configuration\net35\MSBuildTaskHost.pdb")

foreach ($file in $filesToCopyToBin) {
    Copy-WithBackup $([IO.Path]::Combine($PSScriptRoot, "..", $file))
}

Write-Host -ForegroundColor Green "Copy succeeded"
Write-Verbose "Run $destination\MSBuild.exe"