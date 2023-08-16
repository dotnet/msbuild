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
  if (-not ($env:PROCESSOR_ARCHITECTURE -like "arm64"))
  {
  InstallDotNetSharedFramework "1.0.5"
  InstallDotNetSharedFramework "1.1.2"
  InstallDotNetSharedFramework "2.1.0"
  InstallDotNetSharedFramework "2.2.8"
  }
  InstallDotNetSharedFramework "3.1.0"
  InstallDotNetSharedFramework "5.0.0"
  InstallDotNetSharedFramework "6.0.0"
  InstallDotNetSharedFramework "7.0.0"

  CreateBuildEnvScripts
  CreateVSShortcut
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

function CreateVSShortcut()
{
  # https://github.com/microsoft/vswhere/wiki/Installing
  $installerPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
  if(-Not (Test-Path -Path $installerPath))
  {
    return
  }

  $versionFilePath = Join-Path $RepoRoot 'src\Layout\redist\minimumMSBuildVersion'
  # Gets the first digit (ex. 17) and appends '.0' to it.
  $vsMajorVersion = "$(((Get-Content $versionFilePath).Split('.'))[0]).0"
  $devenvPath = (& "$installerPath\vswhere.exe" -all -prerelease -latest -version $vsMajorVersion -find Common7\IDE\devenv.exe) | Select-Object -First 1
  if(-Not $devenvPath)
  {
    return
  }

  $scriptPath = Join-Path $ArtifactsDir 'sdk-build-env.ps1'
  $slnPath = Join-Path $RepoRoot 'sdk.sln'
  $commandToLaunch = "& '$scriptPath'; & '$devenvPath' '$slnPath'"
  $powershellPath = '%SystemRoot%\system32\WindowsPowerShell\v1.0\powershell.exe'
  $shortcutPath = Join-Path $ArtifactsDir 'VS with sdk.sln.lnk'

  # https://stackoverflow.com/a/9701907/294804
  # https://learn.microsoft.com/en-us/troubleshoot/windows-client/admin-development/create-desktop-shortcut-with-wsh
  $wsShell = New-Object -ComObject WScript.Shell
  $shortcut = $wsShell.CreateShortcut($shortcutPath)
  $shortcut.TargetPath = $powershellPath
  $shortcut.Arguments = "-WindowStyle Hidden -Command ""$commandToLaunch"""
  $shortcut.IconLocation = $devenvPath
  $shortcut.WindowStyle = 7 # Minimized
  $shortcut.Save()
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

# Let's clear out the stage-zero folders that map to the current runtime to keep stage 2 clean
function CleanOutStage0ToolsetsAndRuntimes {
  $GlobalJson = Get-Content -Raw -Path (Join-Path $RepoRoot 'global.json') | ConvertFrom-Json
  $dotnetSdkVersion = $GlobalJson.tools.dotnet
  $dotnetRoot = $env:DOTNET_INSTALL_DIR
  $versionPath = Join-Path $dotnetRoot '.version'
  $aspnetRuntimePath = [IO.Path]::Combine( $dotnetRoot, 'shared' ,'Microsoft.AspNetCore.App')
  $coreRuntimePath = [IO.Path]::Combine( $dotnetRoot, 'shared' ,'Microsoft.NETCore.App')
  $wdRuntimePath = [IO.Path]::Combine( $dotnetRoot, 'shared', 'Microsoft.WindowsDesktop.App')
  $sdkPath = Join-Path $dotnetRoot 'sdk'
  $majorVersion = $dotnetSdkVersion.Substring(0,1)

  if (Test-Path($versionPath)) {
    $lastInstalledSDK = Get-Content -Raw -Path ($versionPath)
    if ($lastInstalledSDK -ne $dotnetSdkVersion)
    {
      $dotnetSdkVersion | Out-File -FilePath $versionPath -NoNewline
      Remove-Item (Join-Path $aspnetRuntimePath "$majorVersion.*") -Recurse
      Remove-Item (Join-Path $coreRuntimePath "$majorVersion.*") -Recurse
      Remove-Item (Join-Path $wdRuntimePath "$majorVersion.*") -Recurse
      Remove-Item (Join-Path $sdkPath "$majorVersion.*") -Recurse
      Remove-Item (Join-Path $dotnetRoot "packs") -Recurse
      Remove-Item (Join-Path $dotnetRoot "sdk-manifests") -Recurse
      Remove-Item (Join-Path $dotnetRoot "templates") -Recurse
      throw "Installed a new SDK, deleting existing shared frameworks and sdk folders. Please rerun build"
    }
  }
  else
  {
    $dotnetSdkVersion | Out-File -FilePath $versionPath -NoNewline
  }
}

InitializeCustomSDKToolset

CleanOutStage0ToolsetsAndRuntimes
