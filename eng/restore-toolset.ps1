function InitializeCustomSDKToolset {
  if (-not $restore) {
    return
  }

  CreateBuildEnvScripts
  CreateLocalSdkRunnerScript
  CreateVSShortcut
}

function CreateBuildEnvScripts {
  Create-Directory $ArtifactsDir
  $scriptPath = Join-Path $ArtifactsDir "msbuild-build-env.bat"
  $dotnetPath = "$ArtifactsDir\bin\bootstrap\core"
  $scriptContents = @"
@echo off
title MSBuild ($RepoRoot)
set DOTNET_MULTILEVEL_LOOKUP=0
REM https://aka.ms/vs/unsigned-dotnet-debugger-lib
set VSDebugger_ValidateDotnetDebugLibSignatures=0

set DOTNET_ROOT=$dotnetPath
set DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=$dotnetPath

set PATH=$dotnetPath;%PATH%
set DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0

DOSKEY killdotnet=taskkill /F /IM dotnet.exe /T ^& taskkill /F /IM VSTest.Console.exe /T ^& taskkill /F /IM msbuild.exe /T
"@

  Out-File -FilePath $scriptPath -InputObject $scriptContents -Encoding ASCII

  Create-Directory $ArtifactsDir
  $scriptPath = Join-Path $ArtifactsDir "msbuild-build-env.ps1"
  $scriptContents = @"
`$host.ui.RawUI.WindowTitle = "MSBuild ($RepoRoot)"
`$env:DOTNET_MULTILEVEL_LOOKUP = 0
# https://aka.ms/vs/unsigned-dotnet-debugger-lib
`$env:VSDebugger_ValidateDotnetDebugLibSignatures = 0

`$env:DOTNET_ROOT = "$ArtifactsDir\bin\bootstrap\core"
`$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = "$ArtifactsDir\bin\bootstrap\core"

`$env:PATH = "$ArtifactsDir\bin\bootstrap\core;$env:PATH"
`$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = "0"

function killdotnet {
  taskkill /F /IM dotnet.exe /T
  taskkill /F /IM VSTest.Console.exe /T
  taskkill /F /IM msbuild.exe /T
}
"@

  Out-File -FilePath $scriptPath -InputObject $scriptContents -Encoding ASCII
}

function CreateLocalSdkRunnerScript {
  Create-Directory $ArtifactsDir
  $scriptPath = Join-Path $ArtifactsDir "localsdk.ps1"
  $scriptContents = @"
param()

`$dotnetRoot = "$ArtifactsDir\bin\bootstrap\core"
if (`$IsWindows) {
  `$dotnetBin = Join-Path `$dotnetRoot "dotnet.exe"
} else {
  `$dotnetBin = Join-Path `$dotnetRoot "dotnet"
}

if (-not (Test-Path -Path `$dotnetBin)) {
  Write-Error "localsdk: could not find dotnet at `$dotnetBin. Did you run build.cmd?"
  exit 2
}

# Get all arguments passed to the script
`$RemainingArgs = `$args

if (-not `$RemainingArgs -or `$RemainingArgs.Count -lt 1) {
  Write-Host "Usage: `$(Split-Path -Leaf `$PSCommandPath) <dotnet-command> [args...]"
  Write-Host "Example: `$(Split-Path -Leaf `$PSCommandPath) --info"
  Write-Host "Example: `$(Split-Path -Leaf `$PSCommandPath) msbuild foo.csproj /t:Build"
  exit 1
}

# Save current environment to restore after invoking dotnet
`$saved = @{
  DOTNET_MULTILEVEL_LOOKUP = `$env:DOTNET_MULTILEVEL_LOOKUP
  VSDebugger_ValidateDotnetDebugLibSignatures = `$env:VSDebugger_ValidateDotnetDebugLibSignatures
  DOTNET_ROOT = `$env:DOTNET_ROOT
  DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = `$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR
  PATH = `$env:PATH
  DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = `$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH
}

try {
  `$env:DOTNET_MULTILEVEL_LOOKUP = "0"
  `$env:VSDebugger_ValidateDotnetDebugLibSignatures = "0"
  `$env:DOTNET_ROOT = `$dotnetRoot
  `$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = `$dotnetRoot
  if (`$IsWindows) {
    `$env:PATH = "$ArtifactsDir\bin\bootstrap\core;`$(`$saved.PATH)"
  } else {
    `$env:PATH = "$ArtifactsDir/bin/bootstrap/core:`$(`$saved.PATH)"
  }
  `$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = "0"

  & `$dotnetBin @RemainingArgs
  exit `$LASTEXITCODE
}
finally {
  `$env:DOTNET_MULTILEVEL_LOOKUP = `$saved.DOTNET_MULTILEVEL_LOOKUP
  `$env:VSDebugger_ValidateDotnetDebugLibSignatures = `$saved.VSDebugger_ValidateDotnetDebugLibSignatures
  `$env:DOTNET_ROOT = `$saved.DOTNET_ROOT
  `$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR = `$saved.DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR
  `$env:PATH = `$saved.PATH
  `$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = `$saved.DOTNET_ADD_GLOBAL_TOOLS_TO_PATH
}
"@

  Out-File -FilePath $scriptPath -InputObject $scriptContents -Encoding ASCII
}

function CreateVSShortcut {
  # https://github.com/microsoft/vswhere/wiki/Installing
  $installerPath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer"
  if (-Not (Test-Path -Path $installerPath)) {
    return
  }

  # Read and parse the JSON file
  $jsonContent = Get-Content (Join-Path $RepoRoot 'global.json') | ConvertFrom-Json
  $vsVersion = $jsonContent.tools.vs.version

  # If you want just the major version like the existing code does:
  $vsMajorVersion = "$(($vsVersion.Split('.'))[0]).0"
  $devenvPath = (& "$installerPath\vswhere.exe" -all -prerelease -latest -version $vsMajorVersion -find Common7\IDE\devenv.exe) | Select-Object -First 1
  if (-Not $devenvPath) {
    return
  }

  $scriptPath = Join-Path $ArtifactsDir 'msbuild-build-env.ps1'
  $slnPath = Join-Path $RepoRoot 'MSBuild.sln'
  $commandToLaunch = "& '$scriptPath'; & '$devenvPath' '$slnPath'"
  $powershellPath = '%SystemRoot%\system32\WindowsPowerShell\v1.0\powershell.exe'
  $shortcutPath = Join-Path $ArtifactsDir 'VS with MSBuild.sln.lnk'

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

InitializeCustomSDKToolset
