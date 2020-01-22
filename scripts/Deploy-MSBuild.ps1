[CmdletBinding(PositionalBinding=$false)]
Param(
  [Parameter(Mandatory = $true)]
  [string] $destination,
  [ValidateSet('Debug','Release')]
  [string] $configuration = "Debug",
  [ValidateSet('Core','Desktop')]
  [string] $runtime = "Desktop"
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

if ($runtime -eq "Desktop") {
    $targetFramework = "net472"
} else {
    $targetFramework = "netcoreapp2.1"
}

$filesToCopyToBin = @(
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Build.dll"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Build.Framework.dll"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Build.Tasks.Core.dll"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Build.Utilities.Core.dll"

    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Common.CrossTargeting.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Common.CurrentVersion.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Common.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.CSharp.CrossTargeting.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.CSharp.CurrentVersion.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.CSharp.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Managed.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Managed.Before.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Managed.After.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Net.props"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.NetFramework.CurrentVersion.props"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.NetFramework.CurrentVersion.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.NetFramework.props"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.NetFramework.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.VisualBasic.CrossTargeting.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.VisualBasic.CurrentVersion.targets"
    "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.VisualBasic.targets"
)

if ($runtime -eq "Desktop") {
    $runtimeSpecificFiles = @(
        "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\MSBuild.exe"
        "artifacts\bin\Microsoft.Build.Conversion\$configuration\$targetFramework\Microsoft.Build.Conversion.Core.dll"
        "artifacts\bin\Microsoft.Build.Engine\$configuration\$targetFramework\Microsoft.Build.Engine.dll"

        "artifacts\bin\MSBuildTaskHost\$configuration\net35\MSBuildTaskHost.exe"
        "artifacts\bin\MSBuildTaskHost\$configuration\net35\MSBuildTaskHost.pdb"

        "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Data.Entity.targets"
        "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.ServiceModel.targets"
        "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.WinFx.targets"
        "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.WorkflowBuildExtensions.targets"
        "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Microsoft.Xaml.targets"
        "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Workflow.targets"
        "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\Workflow.VisualBasic.targets"
    )
} else {
    $runtimeSpecificFiles = @(
        "artifacts\bin\MSBuild.Bootstrap\$configuration\$targetFramework\MSBuild.dll"
    )
}

$filesToCopyToBin += $runtimeSpecificFiles

foreach ($file in $filesToCopyToBin) {
    Copy-WithBackup $([IO.Path]::Combine($PSScriptRoot, "..", $file))
}

Write-Host -ForegroundColor Green "Copy succeeded"
Write-Verbose "Run $destination\MSBuild.exe"
