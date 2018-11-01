function InitializeCustomSDKToolset {
  if (-not $restore) {
    return
  }

  # Disable first run since we want to control all package sources
  $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Don't resolve shared frameworks from user or global locations
  $env:DOTNET_MULTILEVEL_LOOKUP=0

  # Turn off MSBuild Node re-use
  $env:MSBUILDDISABLENODEREUSE=1

  # Workaround for the sockets issue when restoring with many nuget feeds.
  $env:DOTNET_SYSTEM_NET_HTTP_USESOCKETSHTTPHANDLER=0

  # Enable vs test console logging
  $env:VSTEST_BUILD_TRACE=1
  $env:VSTEST_TRACE_BUILD=1

  $env:DOTNET_CLI_TELEMETRY_PROFILE=$env:DOTNET_CLI_TELEMETRY_PROFILE;https://github.com/dotnet/cli

  # The following frameworks and tools are used only for testing.
  # Do not attempt to install them in source build.
  if ($env:DotNetBuildFromSource -eq "true" -or $Architecture -ne $InstallArchitecture) {
    return
  }

  InstallDotNetSharedFramework "1.1.2"
  InstallDotNetSharedFramework "2.0.0"
  InstallDotNetSharedFramework "2.1.0"
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