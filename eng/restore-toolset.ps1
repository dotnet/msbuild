function InitializeCustomSDKToolset {
  if ($env:TestFullMSBuild -eq "true") {
     $env:DOTNET_SDK_TEST_MSBUILD_PATH = InitializeVisualStudioMSBuild -install:$true -vsRequirements:$GlobalJson.tools.'vs-opt'
     Write-Host "INFO: Tests will run against full MSBuild in $env:DOTNET_SDK_TEST_MSBUILD_PATH"
  }

  if (-not $restore) {
    return
  }

  # The following frameworks and tools are used only for testing.
  # Do not attempt to install them in source build.
  if ($env:DotNetBuildFromSource -eq "true") {
    return
  }

  $cli = InitializeDotnetCli -install:$true
  InstallDotNetSharedFramework "1.0.5"
  InstallDotNetSharedFramework "1.1.2"
  InstallDotNetSharedFramework "2.1.0"
  InstallDotNetSharedFramework "2.2.8"
  InstallDotNetSharedFramework "3.1.0"
  InstallDotNetSharedFramework "5.0.0"
  InstallDotNetSharedFramework "6.0.0"

  CreateBuildEnvScripts
  InstallNuget
}

function InstallNuGet {
  $NugetInstallDir = Join-Path $ArtifactsDir ".nuget"
  $NugetExe = Join-Path $NugetInstallDir "nuget.exe"

  if (!(Test-Path -Path $NugetExe)) {
    Create-Directory $NugetInstallDir
    Invoke-WebRequest "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe" -UseBasicParsing -OutFile $NugetExe
  }
}

function CreateBuildEnvScripts()
{
  Create-Directory $ArtifactsDir
  $scriptPath = Join-Path $ArtifactsDir "sdk-build-env.bat"
  $scriptContents = @"
@echo off
title SDK Build ($RepoRoot)
set DOTNET_MULTILEVEL_LOOKUP=0

set DOTNET_ROOT=$env:DOTNET_INSTALL_DIR
set DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=$env:DOTNET_INSTALL_DIR

set PATH=$env:DOTNET_INSTALL_DIR;%PATH%
set NUGET_PACKAGES=$env:NUGET_PACKAGES

DOSKEY killdotnet=taskkill /F /IM dotnet.exe /T ^& taskkill /F /IM VSTest.Console.exe /T ^& taskkill /F /IM msbuild.exe /T
"@

  Out-File -FilePath $scriptPath -InputObject $scriptContents -Encoding ASCII

  Create-Directory $ArtifactsDir
  $scriptPath = Join-Path $ArtifactsDir "sdk-build-env.ps1"
  $scriptContents = @"
`$host.ui.RawUI.WindowTitle = "SDK Build ($RepoRoot)"
`$env:DOTNET_MULTILEVEL_LOOKUP=0

`$env:DOTNET_ROOT="$env:DOTNET_INSTALL_DIR"
`$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR="$env:DOTNET_INSTALL_DIR"

`$env:PATH="$env:DOTNET_INSTALL_DIR;" + `$env:PATH
`$env:NUGET_PACKAGES="$env:NUGET_PACKAGES"

function killdotnet {
  taskkill /F /IM dotnet.exe /T
  taskkill /F /IM VSTest.Console.exe /T
  taskkill /F /IM msbuild.exe /T
}
"@

  Out-File -FilePath $scriptPath -InputObject $scriptContents -Encoding ASCII
}

function InstallDotNetSharedFramework([string]$version) {
  $dotnetRoot = $env:DOTNET_INSTALL_DIR
  $fxDir = Join-Path $dotnetRoot "shared\Microsoft.NETCore.App\$version"

  if (!(Test-Path $fxDir)) {
    $installScript = GetDotNetInstallScript $dotnetRoot
    & $installScript -Version $version -InstallDir $dotnetRoot -Runtime "dotnet" -SkipNonVersionedFiles

    if($lastExitCode -ne 0) {
      throw "Failed to install shared Framework $version to '$dotnetRoot' (exit code '$lastExitCode')."
    }
  }
}

InitializeCustomSDKToolset
