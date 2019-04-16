function InitializeCustomSDKToolset {    
  if ($env:TestFullMSBuild -eq "true") {
     $env:DOTNET_SDK_TEST_MSBUILD_PATH = InitializeVisualStudioMSBuild -install:$false -vsRequirements:$GlobalJson.tools.'vs-opt'
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
  InstallDotNetSharedFramework "2.2.4"

  CreateBuildEnvScript
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

function CreateBuildEnvScript()
{
  Create-Directory $ArtifactsDir
  $scriptPath = Join-Path $ArtifactsDir "sdk-build-env.bat"
  $scriptContents = @"
@echo off
title SDK Build ($RepoRoot)
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_MULTILEVEL_LOOKUP=0

set PATH=$env:DOTNET_INSTALL_DIR;%PATH%
set NUGET_PACKAGES=$env:NUGET_PACKAGES
"@

  Out-File -FilePath $scriptPath -InputObject $scriptContents -Encoding ASCII
}

function InstallDotNetSharedFramework([string]$version) {
  $dotnetRoot = $env:DOTNET_INSTALL_DIR
  $fxDir = Join-Path $dotnetRoot "shared\Microsoft.NETCore.App\$version"

  if (!(Test-Path $fxDir)) {
    $installScript = GetDotNetInstallScript $dotnetRoot
    & $installScript -Version $version -InstallDir $dotnetRoot -Runtime "dotnet"

    if($lastExitCode -ne 0) {
      throw "Failed to install shared Framework $version to '$dotnetRoot' (exit code '$lastExitCode')."
    }
  }
}

InitializeCustomSDKToolset