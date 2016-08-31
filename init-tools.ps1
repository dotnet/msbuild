#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string]$Architecture="x64")

$RepoRoot = "$PSScriptRoot"

# Install a stage 0
Write-Host "Installing .NET Core CLI Stage 0 from branchinfo channel"
    
& "$RepoRoot\scripts\obtain\dotnet-install.ps1" -Channel feature-msbuild -Architecture $Architecture -Verbose
if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }

# Put the stage0 on the path
$env:PATH = "$env:DOTNET_INSTALL_DIR;$env:PATH"

# Disable first run since we want to control all package sources
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Setup BuildTools vars
$BUILD_TOOLS_VERSION = Get-Content "$RepoRoot\BuildToolsVersion.txt"
$BUILD_TOOLS_PATH=$RepoRoot + "\build_tools"
$BUILD_TOOLS_SOURCE='https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json'
$BUILD_TOOLS_SEMAPHORE=$BUILD_TOOLS_PATH + "\init-tools.completed"
$INIT_TOOLS_LOG=$RepoRoot + "\init-tools.log"
$PROJECT_JSON_FILE=$BUILD_TOOLS_PATH + "\project.json"
$PROJECT_JSON_CONTENTS="{ `"dependencies`": { `"Microsoft.DotNet.BuildTools`": `"" + $BUILD_TOOLS_VERSION + "`" }, `"frameworks`": { `"netcoreapp1.0`": { } } }"
$PACKAGES_DIR=$RepoRoot + "\.nuget\packages"
$BUILD_TOOLS_PACKAGE_PATH=$PACKAGES_DIR + "\Microsoft.DotNet.BuildTools\" + $BUILD_TOOLS_VERSION + "\lib"
$DOTNET_EXE_CMD=$env:DOTNET_INSTALL_DIR + "\dotnet.exe"

# If build tools are already installed, escape
if (Test-Path "$BUILD_TOOLS_SEMAPHORE")
{
    Write-Host "Tools are already initialized"
    exit 0
}

# Check for build tools
if (!(Test-Path "$BUILD_TOOLS_PATH"))
{
    mkdir "$BUILD_TOOLS_PATH" | Out-Null
}

# Write the build tools project.json file
"$PROJECT_JSON_CONTENTS" | Set-Content "$PROJECT_JSON_FILE"

# Restore build tools
$args="restore $PROJECT_JSON_FILE --packages $PACKAGES_DIR --source $BUILD_TOOLS_SOURCE"
Start-Process -FilePath $DOTNET_EXE_CMD -ArgumentList $args -Wait -RedirectStandardOutput $INIT_TOOLS_LOG -NoNewWindow
if (!(Test-Path "$BUILD_TOOLS_PACKAGE_PATH\init-tools.cmd"))
{
    Write-Host "ERROR: Could not restore build tools correctly. See '$INIT_TOOLS_LOG' for more details"
    exit 1
}

# Bring down the CLI for build tools
$DOTNET_PATH=$BUILD_TOOLS_PATH + "\dotnetcli"

Write-Host "Installing Build Tools CLI version..."
if (!(Test-Path "$DOTNET_PATH"))
{
     mkdir "$DOTNET_PATH"
}

$DOTNET_VERSION = Get-Content "$RepoRoot\BuildToolsCliVersion.txt"
$DOTNET_LOCAL_PATH=$DOTNET_PATH
& "$RepoRoot\scripts\obtain\dotnet-install.ps1" -Channel "rel-1.0.0" -Version "$DOTNET_VERSION" -InstallDir "$DOTNET_LOCAL_PATH"

if (!(Test-Path "$DOTNET_LOCAL_PATH"))
{
    Write-Host "Could not install Build Tools CLI version correctly"
    exit 1
}

# Initialize build tools
cmd /c "$BUILD_TOOLS_PACKAGE_PATH\init-tools.cmd $RepoRoot $DOTNET_LOCAL_PATH\dotnet.exe $BUILD_TOOLS_PATH" >> "$INIT_TOOLS_LOG"
Write-Host "Done initializing tools."
Write-Host "Init-Tools completed for BuildTools Version: $BUILD_TOOLS_VERSION" > $BUILD_TOOLS_SEMAPHORE
