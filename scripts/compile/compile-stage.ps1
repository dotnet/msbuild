#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [Parameter(Mandatory=$true)][string]$Tfm,
    [Parameter(Mandatory=$true)][string]$Rid,
    [Parameter(Mandatory=$true)][string]$Configuration,
    [Parameter(Mandatory=$true)][string]$StageOutputDir,
    [Parameter(Mandatory=$true)][string]$RepoRoot,
    [Parameter(Mandatory=$true)][string]$HostDir,
    [Parameter(Mandatory=$true)][string]$CompilationOutputDir)

. $REPOROOT\scripts\package\projectsToPack.ps1

$Projects = loadBuildProjectList

$BinariesForCoreHost = @(
    "csi"
    "csc"
    "vbc"
)

$FilesToClean = @(
    "README.md"
    "Microsoft.DotNet.Runtime.exe"
    "Microsoft.DotNet.Runtime.dll"
    "Microsoft.DotNet.Runtime.deps"
    "Microsoft.DotNet.Runtime.pdb"
)

$RuntimeOutputDir = "$StageOutputDir\runtime\coreclr"

# Publish each project
$Projects | ForEach-Object {
    dotnet publish --native-subdirectory --framework "$Tfm" --runtime "$Rid" --output "$StageOutputDir\bin" --configuration "$Configuration" "$RepoRoot\src\$($_.ProjectName)"
    if (!$?) {
        Write-Host Command failed: dotnet publish --native-subdirectory --framework "$Tfm" --runtime "$Rid" --output "$StageOutputDir\bin" --configuration "$Configuration" "$RepoRoot\src\$($_.ProjectName)"
        exit 1
    }
}

# Publish the runtime
dotnet publish --framework "$Tfm" --runtime "$Rid" --output "$RuntimeOutputDir" --configuration "$Configuration" "$RepoRoot\src\Microsoft.DotNet.Runtime"
if (!$?) {
    Write-Host Command failed: dotnet publish --framework "$Tfm" --runtime "$Rid" --output "$RuntimeOutputDir" --configuration "$Configuration" "$RepoRoot\src\Microsoft.DotNet.Runtime"
    Exit 1
}

# Build the projects that we are going to ship as nuget packages
$ProjectsToPack | ForEach-Object {
    dotnet build --output "$CompilationOutputDir\forPackaging" --configuration "$Configuration" "$RepoRoot\src\$_"
    if (!$?) {
        Write-Host Command failed: dotnet build --native-subdirectory --output "$CompilationOutputDir\forPackaging" --configuration "$Configuration" "$RepoRoot\src\$_"
        exit 1
    }
}

# Clean up bogus additional files
$FilesToClean | ForEach-Object {
    $path = Join-Path $RuntimeOutputDir $_
    if (Test-Path $path) {
        del -for $path
    }
}

# Copy the runtime app-local for the tools
cp -rec "$RuntimeOutputDir\*" "$StageOutputDir\bin" -ErrorVariable capturedErrors -ErrorAction SilentlyContinue
$capturedErrors | foreach-object {
    if ($_ -notmatch "already exists") {
        write-error $_
        Exit 1
    }
}

# Deploy the CLR host to the output
cp "$HostDir\corehost.exe" "$StageOutputDir\bin"
cp "$HostDir\hostpolicy.dll" "$StageOutputDir\bin"

# corehostify externally-provided binaries (csc, vbc, etc.)
$BinariesForCoreHost | ForEach-Object {
    mv $StageOutputDir\bin\$_.exe $StageOutputDir\bin\$_.dll -Force
    cp $StageOutputDir\bin\corehost.exe $StageOutputDir\bin\$_.exe -Force
}

# Crossgen Roslyn
#if (-not (Test-Path "$StageOutputDir\bin\csc.ni.exe")) {
    #header "Crossgening Roslyn compiler ..."
    #_cmd "$RepoRoot\scripts\crossgen\crossgen_roslyn.cmd ""$StageOutputDir"""
#}

# Copy in AppDeps
header "Acquiring Native App Dependencies"
_ "$RepoRoot\scripts\build\build_appdeps.ps1" @("$RepoRoot", "$StageOutputDir")

