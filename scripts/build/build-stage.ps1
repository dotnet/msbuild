#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [Parameter(Mandatory=$true)][string]$Tfm,
    [Parameter(Mandatory=$true)][string]$Rid,
    [Parameter(Mandatory=$true)][string]$Configuration,
    [Parameter(Mandatory=$true)][string]$OutputDir,
    [Parameter(Mandatory=$true)][string]$RepoRoot,
    [Parameter(Mandatory=$true)][string]$HostDir)

$Projects = @(
    "Microsoft.DotNet.Cli",
    "Microsoft.DotNet.Tools.Compiler",
    "Microsoft.DotNet.Tools.Compiler.Csc",
    "Microsoft.DotNet.Tools.Pack",
    "Microsoft.DotNet.Tools.Publish",
    "Microsoft.DotNet.Tools.Repl",
    "Microsoft.DotNet.Tools.Repl.Csi",
    "Microsoft.DotNet.Tools.Resgen",
    "Microsoft.DotNet.Tools.Run",
    "Microsoft.DotNet.Tools.Init",
    "Microsoft.DotNet.Tools.Compiler.Native"
)

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

if (Test-Path $OutputDir) {
    del -rec -for $OutputDir
}

$RuntimeOutputDir = "$OutputDir\runtime\coreclr"

# Publish each project
$Projects | ForEach-Object {
    dotnet publish --framework "$Tfm" --runtime "$Rid" --output "$OutputDir\bin" --configuration "$Configuration" "$RepoRoot\src\$_"
    if (!$?) {
        Write-Host Command failed: dotnet publish --framework "$Tfm" --runtime "$Rid" --output "$OutputDir\bin" --configuration "$Configuration" "$RepoRoot\src\$_"
        exit 1
    }
}

# Publish the runtime
dotnet publish --framework "$Tfm" --runtime "$Rid" --output "$RuntimeOutputDir" --configuration "$Configuration" "$RepoRoot\src\Microsoft.DotNet.Runtime"
if (!$?) {
    Write-Host Command failed: dotnet publish --framework "$Tfm" --runtime "$Rid" --output "$RuntimeOutputDir" --configuration "$Configuration" "$RepoRoot\src\Microsoft.DotNet.Runtime"
    Exit 1
}


# Clean up bogus additional files
$FilesToClean | ForEach-Object {
    $path = Join-Path $RuntimeOutputDir $_
    if (Test-Path $path) {
        del -for $path
    }
}

# Copy the runtime app-local for the tools
cp -rec "$RuntimeOutputDir\*" "$OutputDir\bin"

# Deploy the CLR host to the output
cp "$HostDir\corehost.exe" "$OutputDir\bin"

# corehostify externally-provided binaries (csc, vbc, etc.)
$BinariesForCoreHost | ForEach-Object {
    mv $OutputDir\bin\$_.exe $OutputDir\bin\$_.dll
    cp $OutputDir\bin\corehost.exe $OutputDir\bin\$_.exe
}

# remove any deps files that got brought along (they aren't needed because we have an app-local runtime and dependencies)
del $OutputDir\bin\*.deps
