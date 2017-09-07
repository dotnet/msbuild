# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [string]$InputMsi,
    [string]$DotnetDir,
    [string]$TestDir
)

. "$PSScriptRoot\..\..\scripts\common\_common.ps1"

$RepoRoot = Convert-Path "$PSScriptRoot\..\.."

function CopyInstaller([string]$destination)
{
    # Copy both the .msi and the .exe to the testBin directory so
    # the tests running in the docker container have access to them.
    Copy-Item $inputMsi -Destination:$destination

    $BundlePath = [System.IO.Path]::ChangeExtension($inputMsi, "exe")
    Copy-Item $BundlePath -Destination:$destination
}

function CopyTestXUnitRunner([string]$destination)
{
    $XUnitRunnerDir = Join-Path $env:NUGET_PACKAGES xunit.runner.console\2.1.0\tools

    Copy-Item $XUnitRunnerDir\xunit.console.exe -Destination:$destination
    Copy-Item $XUnitRunnerDir\xunit.runner.utility.desktop.dll -Destination:$destination
}

Write-Output "Running tests for MSI installer at $inputMsi."

if(!(Test-Path $inputMsi))
{
    throw "$inputMsi not found" 
}

$testName = "Microsoft.DotNet.Cli.Msi.Tests"
$testProj="$PSScriptRoot\$testName\$testName.csproj"
$testBin="$TestDir\$testName"

pushd "$DotnetDir"

try {
    .\dotnet restore `
        $testProj | Out-Host

    if($LastExitCode -ne 0)
    {
        throw "dotnet restore failed with exit code $LastExitCode."     
    }

    .\dotnet publish `
        --framework net46 `
        --output $testBin `
        $testProj | Out-Host

    if($LastExitCode -ne 0)
    {
        throw "dotnet publish failed with exit code $LastExitCode."     
    }

    if($env:RunInstallerTestsInDocker)
    {
        CopyInstaller $testBin
        CopyTestXUnitRunner $testBin

        Write-Output "Running installer tests in Windows Container"

        # --net="none" works around a networking issue on the containers on the CI machines.
        # Since our installer tests don't require the network, it is fine to shut it off.
        $MsiFileName = [System.IO.Path]::GetFileName($inputMsi)
        docker run `
            --rm `
            -v "$testBin\:D:" `
            -e "CLI_MSI=D:\$MsiFileName" `
            --net="none" `
            windowsservercore `
            D:\xunit.console.exe D:\$testName.dll | Out-Host

        if($LastExitCode -ne 0)
        {
            throw "xunit runner failed with exit code $LastExitCode."       
        }
    }
}
finally {
    popd
}

Exit 0
