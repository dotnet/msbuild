#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param([string]$Configuration = "Debug",
      [string]$Offline = $false)

$ErrorActionPreference="Stop"

. $PSScriptRoot\_common.ps1

# Capture PATH for later
$StartPath = $env:PATH
$StartDotNetHome = $env:DOTNET_HOME

try {

    # Check prereqs
    if (!(Get-Command -ErrorAction SilentlyContinue cmake)) {
        throw @"
cmake is required to build the native host 'corehost'"
Download it from https://www.cmake.org
"@
    }

    if($Offline){
        Write-Host "Skipping Stage 0, Dnx, and Packages dowlnoad: Offline build"
    }
    else {
        # Install a stage 0
        header "Installing dotnet stage 0"
        & "$PSScriptRoot\install.ps1"
        if (!$?) {
            Write-Host "Command failed: $PSScriptRoot\install.ps1"
            Exit 1
        }
    
        # Put stage 0 on the path
        $DotNetTools = $env:DOTNET_INSTALL_DIR
        if (!$DotNetTools) {
            $DotNetTools = "$($env:LOCALAPPDATA)\Microsoft\dotnet"
        }
    
        # Download dnx to copy to stage2
        if ((Test-Path "$DnxDir")) {
            Remove-Item -Recurse -Force $DnxDir
        }
        mkdir "$DnxDir" | Out-Null
        $DnxUrl="https://api.nuget.org/packages/dnx-coreclr-win-x64.$DnxVersion.nupkg"
        Invoke-WebRequest -UseBasicParsing "$DnxUrl" -OutFile "$DnxDir\dnx.zip"
        Add-Type -Assembly System.IO.Compression.FileSystem | Out-Null
        [System.IO.Compression.ZipFile]::ExtractToDirectory("$DnxDir\dnx.zip", "$DnxDir")
        $DnxRoot = "$DnxDir/bin"
    
        # Restore packages
        header "Restoring packages"
        & "$DnxRoot\dnu" restore "$RepoRoot" --quiet --runtime "osx.10.10-x64" --runtime "ubuntu.14.04-x64" --runtime "win7-x64" --no-cache
        if (!$?) {
            Write-Host "Command failed: " dotnet restore "$RepoRoot" --quiet --runtime "osx.10.10-x64" --runtime "ubuntu.14.04-x64" --runtime "win7-x64" --no-cache
            Exit 1
        }
    }

    header "Building corehost"
    pushd "$RepoRoot\src\corehost"
    try {
        if (!(Test-Path "cmake\$Rid")) {
            mkdir "cmake\$Rid" | Out-Null
        }
        cd "cmake\$Rid"
        cmake ..\.. -G "Visual Studio 14 2015 Win64"
        $pf = $env:ProgramFiles
        if (Test-Path "env:\ProgramFiles(x86)") {
            $pf = (cat "env:\ProgramFiles(x86)")
        }
        & "$pf\MSBuild\14.0\Bin\MSBuild.exe" ALL_BUILD.vcxproj /p:Configuration="$Configuration"
        if (!$?) {
            Write-Host "Command failed: $pf\MSBuild\14.0\Bin\MSBuild.exe" ALL_BUILD.vcxproj /p:Configuration="$Configuration"
            Exit 1
        }

        if (!(Test-Path $HostDir)) {
            mkdir $HostDir | Out-Null
        }
        cp "$RepoRoot\src\corehost\cmake\$Rid\$Configuration\corehost.exe" $HostDir

        if (Test-Path "$RepoRoot\src\corehost\cmake\$Rid\$Configuration\corehost.pdb")
        {
            cp "$RepoRoot\src\corehost\cmake\$Rid\$Configuration\corehost.pdb" $HostDir
        }
    } finally {
        popd
    }

    # Build Stage 1
    header "Building stage1 dotnet using downloaded stage0 ..."
    & "$PSScriptRoot\build\build-stage.ps1" -Tfm:$Tfm -Rid:$Rid -Configuration:$Configuration -OutputDir:$Stage1Dir -RepoRoot:$RepoRoot -HostDir:$HostDir
    if (!$?) {
        Write-Host "Command failed: $PSScriptRoot\build\build-stage.ps1" -Tfm:$Tfm -Rid:$Rid -Configuration:$Configuration -OutputDir:$Stage1Dir -RepoRoot:$RepoRoot -HostDir:$HostDir
        Exit 1
    }


    # Build Stage 2 using Stage 1
    $env:PATH = "$Stage1Dir\bin;$StartPath"
    header "Building stage2 dotnet using just-built stage1 ..."
    & "$PSScriptRoot\build\build-stage.ps1" -Tfm:$Tfm -Rid:$Rid -Configuration:$Configuration -OutputDir:$Stage2Dir -RepoRoot:$RepoRoot -HostDir:$HostDir
    if (!$?) {
        Write-Host "Command failed: $PSScriptRoot\build\build-stage.ps1" -Tfm:$Tfm -Rid:$Rid -Configuration:$Configuration -OutputDir:$Stage2Dir -RepoRoot:$RepoRoot -HostDir:$HostDir
        Exit 1
    }


    # Crossgen Roslyn
    header "Crossgening Roslyn compiler ..."
    cmd /c "$PSScriptRoot\crossgen\crossgen_roslyn.cmd" "$Stage2Dir"
    if (!$?) {
        Write-Host "Command failed: " cmd /c "$PSScriptRoot\crossgen\crossgen_roslyn.cmd" "$Stage2Dir"
        Exit 1
    }


    # Copy dnx into stage 2
    cp -rec "$DnxRoot\" "$Stage2Dir\bin\dnx\"

    # Copy in the dotnet-restore script
    cp "$PSScriptRoot\dotnet-restore.cmd" "$Stage2Dir\bin\dotnet-restore.cmd"

    # Copy in AppDeps
    $env:PATH = "$Stage2Dir\bin;$StartPath"
    header "Acquiring Native App Dependencies"
    cmd /c "$PSScriptRoot\build\build_appdeps.cmd" "$Stage2Dir"
    if (!$?) {
        Write-Host "Command failed: " cmd /c "$PSScriptRoot\build\build_appdeps.cmd" "$Stage2Dir"
        Exit 1
    }

    # Smoke test stage2
    $env:DOTNET_HOME = "$Stage2Dir"
    & "$PSScriptRoot\test\smoke-test.ps1"
    if (!$?) {
        Write-Host "Command failed: $PSScriptRoot\test\smoke-test.ps1"
        Exit 1
    }

    # E2E Test of stage2
    & "$PSScriptRoot\test\e2e-test.ps1"
    if (!$?) {
        Write-Host "Command failed: $PSScriptRoot\test\e2e-test.ps1"
        Exit 1
    }

    # Run Validation for Project.json dependencies
    dotnet publish $RepoRoot\tools\MultiProjectValidator -o $Stage2Dir\..\tools
    & "$Stage2Dir\..\tools\pjvalidate" "$RepoRoot\src"
    # TODO For release builds, this should be uncommented and fail.
    # if (!$?) {
    #     Write-Host "Project Validation Failed"
    #     Exit 1
    # }

} finally {
    $env:PATH = $StartPath
    $env:DOTNET_HOME = $StartDotNetHome
}
