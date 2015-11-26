# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.

param(
    [string]$inputMsi = $(throw "Specify the full path to the msi which needs to be tested")
)

. "$PSScriptRoot\..\..\scripts\_common.ps1"

function Test-Administrator  
{  
    $user = [Security.Principal.WindowsIdentity]::GetCurrent();
    (New-Object Security.Principal.WindowsPrincipal $user).IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)  
}

Write-Host "Running tests for MSI installer at $inputMsi.."

if(!(Test-Path $inputMsi))
{
    throw "$inputMsi not found" 
}

$env:CLI_MSI=$inputMsi
$testBin="$RepoRoot\artifacts\tests\Dotnet.Cli.Msi.Tests"
$xunitRunner="$env:USERPROFILE\.dnx\packages\xunit.runner.console\2.1.0\tools\xunit.console.exe"

pushd "$Stage2Dir\bin"

try {
    .\dotnet restore `
        --runtime win-anycpu `
        $RepoRoot\packaging\windows\Dotnet.Cli.Msi.Tests\project.json `
        -f https://www.myget.org/F/dotnet-buildtools/api/v3/index.json | Out-Host

    if($LastExitCode -ne 0)
    {
        throw "dotnet restore failed with exit code $LastExitCode."     
    }

    .\dotnet publish `
        --framework net46 `
        --runtime win-anycpu `
        --output $testBin `
        $RepoRoot\packaging\windows\Dotnet.Cli.Msi.Tests\project.json | Out-Host

    if($LastExitCode -ne 0)
    {
        throw "dotnet publish failed with exit code $LastExitCode."     
    }
<#
    if(-Not (Test-Administrator))
    {
        Write-Host -ForegroundColor Yellow "Current script testmsi.ps1 is not run as admin."
        Write-Host -ForegroundColor Yellow "Executing MSI tests require admin privileges."
        Write-Host -ForegroundColor Yellow "Failing silently."
        Exit 0
    }
    
    & $xunitRunner $testBin\Dotnet.Cli.Msi.Tests.exe | Out-Host

    if($LastExitCode -ne 0)
    {
        throw "xunit runner failed with exit code $LastExitCode."       
    }
#>
}
finally {
    popd
}

Exit 0
