#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. $PSScriptRoot\..\common\_common.ps1

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
    $BuildConfiguration = $Configuration
    if ($Configuration -eq "Release") {
        $BuildConfiguration = "RelWithDebInfo"
    }
    & "$pf\MSBuild\14.0\Bin\MSBuild.exe" ALL_BUILD.vcxproj /p:Configuration="$BuildConfiguration"
    if (!$?) {
        Write-Host "Command failed: $pf\MSBuild\14.0\Bin\MSBuild.exe" ALL_BUILD.vcxproj /p:Configuration="$BuildConfiguration"
        Exit 1
    }

    if (!(Test-Path $HostDir)) {
        mkdir $HostDir | Out-Null
    }
    cp "$RepoRoot\src\corehost\cmake\$Rid\cli\$BuildConfiguration\corehost.exe" $HostDir
    cp "$RepoRoot\src\corehost\cmake\$Rid\cli\dll\$BuildConfiguration\hostpolicy.dll" $HostDir

    if (Test-Path "$RepoRoot\src\corehost\cmake\$Rid\cli\$BuildConfiguration\corehost.pdb")
    {
        cp "$RepoRoot\src\corehost\cmake\$Rid\cli\$BuildConfiguration\corehost.pdb" $HostDir
    }
    if (Test-Path "$RepoRoot\src\corehost\cmake\$Rid\cli\dll\$BuildConfiguration\hostpolicy.pdb")
    {
        cp "$RepoRoot\src\corehost\cmake\$Rid\cli\dll\$BuildConfiguration\hostpolicy.pdb" $HostDir
    }
} finally {
    popd
}
