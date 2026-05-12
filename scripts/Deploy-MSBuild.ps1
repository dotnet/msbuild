[CmdletBinding(PositionalBinding=$false)]
Param(
  [Parameter(Mandatory = $true)]
  [string] $destination,
  [ValidateSet('Debug','Release')]
  [string] $configuration = "Debug",
  [ValidateSet('Core','Desktop', 'Detect', 'Full')]
  [string] $runtime = "Detect",
  [string] $binDirectory = "",
  [bool] $makeBackup = $true
)

Set-StrictMode -Version "Latest"
$ErrorActionPreference = "Stop"

function Copy-WithBackup ($origin, $destinationSubFolder = "") {
    $directoryPart = [IO.Path]::Combine($destination, $destinationSubFolder, $origin.IntermediaryDirectories)
    $destinationPath = Join-Path -Path $directoryPart (Split-Path $origin.SourceFile -leaf)

    if (($makeBackup) -and (Test-Path $destinationPath -PathType Leaf)) {
        $backupInto = [IO.Path]::Combine($BackupFolder, $destinationSubFolder)

        # Back up previous copy of the file
        if (!(Test-Path $backupInto)) {
            [system.io.directory]::CreateDirectory($backupInto)
        }
        Copy-Item $destinationPath $backupInto -ErrorAction Stop
    }

    if (!(Test-Path $directoryPart)) {
        [system.io.directory]::CreateDirectory($directoryPart)
    }

    Copy-Item $origin.SourceFile $destinationPath -ErrorAction Stop

    echo "Copied $($origin.SourceFile) to $destinationPath"
}

function FileToCopy([string] $sourceFileRelativeToRepoRoot, [string] $intermediaryDirectories)
{
    return [PSCustomObject]@{"SourceFile"=$([IO.Path]::Combine($PSScriptRoot, "..", $sourceFileRelativeToRepoRoot)); "IntermediaryDirectories"=$intermediaryDirectories}
}

# TODO: find destination in PATH if not specified

# TODO: identify processes likely to be using target MSBuild and warn/offer to kill

# TODO: find most-recently-built MSBuild and make it default $configuration

Write-Verbose "Copying $configuration MSBuild to $destination"

if ($makeBackup) {
    $BackupFolder = New-Item (Join-Path $destination -ChildPath "Backup-$(Get-Date -Format FileDateTime)") -itemType directory -ErrorAction Stop
    Write-Host "Existing MSBuild assemblies backed up to $BackupFolder"
}
else {
    Write-Host "Existing MSBuild assemblies won't be backed up"
}

if ($runtime -eq "Detect") {
    if ($destination -like "*dotnet*sdk*") {
        $runtime = "Core"
        Write-Host "Detected path that looks like an sdk. Writing .NET Core assemblies."
    }
    else {
        $runtime = "Desktop"
        Write-Host "Detected path that does not look like an sdk. Writing .NET Framework assemblies."
    }
}
else {
    if ($runtime -eq "Full") {
        $runtime = "Desktop"
    }
}

if ($runtime -eq "Desktop") {
    $targetFramework = "net472"
} else {
    $targetFramework = "net9.0"
}

# If bootstrap directory is not defined in parameters, use the default location
if ($binDirectory -eq "") {
    $binDirectory = "artifacts\bin" 
}

$bootstrapBinDirectory = "$binDirectory\MSBuild.Bootstrap\$configuration\$targetFramework"

$filesToCopyToBin = @(
    FileToCopy "$bootstrapBinDirectory\Microsoft.Build.dll"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Build.Framework.dll"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Build.Tasks.Core.dll"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Build.Utilities.Core.dll"
    FileToCopy "$bootstrapBinDirectory\Microsoft.NET.StringTools.dll"

    FileToCopy "$bootstrapBinDirectory\Microsoft.Common.CrossTargeting.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Common.CurrentVersion.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Common.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.CSharp.CrossTargeting.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.CSharp.CurrentVersion.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.CSharp.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Managed.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Managed.Before.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.Managed.After.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.NET.props"
    FileToCopy "$bootstrapBinDirectory\Microsoft.NETFramework.CurrentVersion.props"
    FileToCopy "$bootstrapBinDirectory\Microsoft.NETFramework.CurrentVersion.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.NETFramework.props"
    FileToCopy "$bootstrapBinDirectory\Microsoft.NETFramework.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.VisualBasic.CrossTargeting.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.VisualBasic.CurrentVersion.targets"
    FileToCopy "$bootstrapBinDirectory\Microsoft.VisualBasic.targets"

    FileToCopy "$bootstrapBinDirectory\Microsoft.Common.tasks"
)

if ($runtime -eq "Desktop") {
    $runtimeSpecificFiles = @(
        FileToCopy "$binDirectory\Microsoft.Build.Conversion\$configuration\$targetFramework\Microsoft.Build.Conversion.Core.dll"
        FileToCopy "$binDirectory\Microsoft.Build.Engine\$configuration\$targetFramework\Microsoft.Build.Engine.dll"

        FileToCopy "$bootstrapBinDirectory\Microsoft.Bcl.AsyncInterfaces.dll"
        FileToCopy "$bootstrapBinDirectory\Microsoft.Data.Entity.targets"
        FileToCopy "$bootstrapBinDirectory\Microsoft.IO.Redist.dll"
        FileToCopy "$bootstrapBinDirectory\Microsoft.ServiceModel.targets"
        FileToCopy "$bootstrapBinDirectory\Microsoft.WinFx.targets"
        FileToCopy "$bootstrapBinDirectory\Microsoft.WorkflowBuildExtensions.targets"
        FileToCopy "$bootstrapBinDirectory\Microsoft.Xaml.targets"
        FileToCopy "$bootstrapBinDirectory\Workflow.targets"
        FileToCopy "$bootstrapBinDirectory\Workflow.VisualBasic.targets"

        FileToCopy "$bootstrapBinDirectory\System.Buffers.dll"
        FileToCopy "$bootstrapBinDirectory\System.Collections.Immutable.dll"
        FileToCopy "$bootstrapBinDirectory\System.Memory.dll"
        FileToCopy "$bootstrapBinDirectory\System.Numerics.Vectors.dll"
        FileToCopy "$bootstrapBinDirectory\System.Reflection.MetadataLoadContext.dll"
        FileToCopy "$bootstrapBinDirectory\System.Resources.Extensions.dll"
        FileToCopy "$bootstrapBinDirectory\System.Runtime.CompilerServices.Unsafe.dll"
        FileToCopy "$bootstrapBinDirectory\System.Text.Encodings.Web.dll"
        FileToCopy "$bootstrapBinDirectory\System.Text.Json.dll"
        FileToCopy "$bootstrapBinDirectory\System.Threading.Tasks.Dataflow.dll"
        FileToCopy "$bootstrapBinDirectory\System.Threading.Tasks.Extensions.dll"
        FileToCopy "$bootstrapBinDirectory\System.ValueTuple.dll"
    )
} else {
    $runtimeSpecificFiles = @(
        FileToCopy "$bootstrapBinDirectory\MSBuild.dll"
    )
}

if ($runtime -eq "Desktop") {
    $x86files = @(
        FileToCopy "$bootstrapBinDirectory\MSBuild.exe"
        FileToCopy "$bootstrapBinDirectory\MSBuild.exe.config"
        FileToCopy "$binDirectory\MSBuildTaskHost\$configuration\net35\MSBuildTaskHost.exe"
        FileToCopy "$binDirectory\MSBuildTaskHost\$configuration\net35\MSBuildTaskHost.pdb"
    )
    $amd64files = @(
        FileToCopy "$binDirectory\MSBuild\x64\$configuration\$targetFramework\MSBuild.exe"
        FileToCopy "$binDirectory\MSBuild\x64\$configuration\$targetFramework\MSBuild.exe.config"
        FileToCopy "$binDirectory\MSBuildTaskHost\x64\$configuration\net35\MSBuildTaskHost.exe"
        FileToCopy "$binDirectory\MSBuildTaskHost\x64\$configuration\net35\MSBuildTaskHost.pdb"
    )
}

$filesToCopyToBin += $runtimeSpecificFiles

foreach ($file in $filesToCopyToBin) {
    Copy-WithBackup $file
}

if ($runtime -eq "Desktop") {
    foreach ($file in $x86files) {
        Copy-WithBackup $file
    }

    foreach ($file in $filesToCopyToBin) {
        Copy-WithBackup $file "amd64"
    }

    foreach ($file in $amd64files) {
        Copy-WithBackup $file "amd64"
    }
}

Write-Host -ForegroundColor Green "Copy succeeded"
Write-Verbose "Run $destination\MSBuild.exe"
