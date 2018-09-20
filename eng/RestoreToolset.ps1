function InitializeCustomSDKToolset {    
  if ($fullMSBuild) {
    if (!($env:VSInstallDir)) {
      $env:VSInstallDir = LocateVisualStudio
    }

    $env:DOTNET_SDK_TEST_MSBUILD_PATH = Join-Path $env:VSInstallDir "MSBuild\15.0\Bin\msbuild.exe"
  }

  if ($dogfood)
  {
    $env:SDK_REPO_ROOT = $RepoRoot
    $env:SDK_CLI_VERSION = $DotNetCliVersion
    $env:MSBuildSDKsPath = Join-Path $ArtifactsConfigurationDir "bin\Sdks"
    $env:DOTNET_MSBUILD_SDK_RESOLVER_SDKS_DIR = $env:MSBuildSDKsPath
    $env:NETCoreSdkBundledVersionsProps = Join-Path $env:DOTNET_INSTALL_DIR "sdk\$DotNetCliVersion\Microsoft.NETCoreSdk.BundledVersions.props"
    $env:MicrosoftNETBuildExtensionsTargets = Join-Path $env:MSBuildSDKsPath "Microsoft.NET.Build.Extensions\msbuildExtensions\Microsoft\Microsoft.NET.Build.Extensions\Microsoft.NET.Build.Extensions.targets"
 
    if ($properties -eq $null -and $env:DOTNET_SDK_DOGFOOD_SHELL -ne $null)
    {
      $properties = , $env:DOTNET_SDK_DOGFOOD_SHELL
    }
    if ($properties -ne $null)
    {
      $Host.UI.RawUI.WindowTitle = "SDK Test ($RepoRoot) ($configuration)"
      & $properties[0] $properties[1..($properties.Length-1)]
    }
  }

  if (-not $restore) {
    return
  }

  # The following frameworks and tools are used only for testing.
  # Do not attempt to install them in source build.
  if ($env:DotNetBuildFromSource -eq "true") {
    return
  }
  
  $dotnetRoot = $env:DOTNET_INSTALL_DIR

  InstallDotNetSharedFramework $dotnetRoot "1.0.5"
  InstallDotNetSharedFramework $dotnetRoot "1.1.2"
  InstallDotNetSharedFramework $dotnetRoot "2.1.0"

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

function InstallDotNetSharedFramework([string]$dotnetRoot, [string]$version) {
  $fxDir = Join-Path $dotnetRoot "shared\Microsoft.NETCore.App\$version"

  if (!(Test-Path $fxDir)) {
    $installScript = GetDotNetInstallScript $dotnetRoot
    & $installScript -Version $version -InstallDir $dotnetRoot -SharedRuntime

    if($lastExitCode -ne 0) {
      throw "Failed to install shared Framework $version to '$dotnetRoot' (exit code '$lastExitCode')."
    }
  }
}

InitializeCustomSDKToolset