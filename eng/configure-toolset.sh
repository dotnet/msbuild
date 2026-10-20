if [[ -n "${_BuildToolPath:-}" || -n "${_InitializeBuildTool:-}" || -n "${_InitializeToolset:-}" ||
      "${from_vmr:-false}" == true || "${clean:-false}" == true ]]; then
  return
fi

# Keep both SDKs in the repo-local installation unless the caller supplies one.
use_installed_dotnet_cli=false
InitializeDotNetCli "$restore"
ReadGlobalVersion "version"
source_sdk_version="$_ReadGlobalVersion"
if [ ! -d "$_InitializeDotNetCli/sdk/$source_sdk_version" ]; then
  if [ "$restore" != true ]; then
    Write-PipelineTelemetryError -category 'InitializeToolset' "SDK '$source_sdk_version' is missing from '$_InitializeDotNetCli'. Run with --restore."
    ExitWithExitCode 1
  fi

  InstallDotNetSdk "$_InitializeDotNetCli" "$source_sdk_version"
fi

export DOTNET_ROOT="$_InitializeDotNetCli"
export DOTNET_HOST_PATH="$_InitializeDotNetCli/dotnet"
ReadGlobalVersion "dotnet"
_InitializeBuildTool="$DOTNET_HOST_PATH"
_InitializeBuildToolCommand="$_InitializeDotNetCli/sdk/$_ReadGlobalVersion/MSBuild.dll"
