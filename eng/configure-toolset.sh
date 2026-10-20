if [[ -n "${_BuildToolPath:-}" || -n "${_InitializeBuildTool:-}" || -n "${_InitializeToolset:-}" ||
      "${from_vmr:-false}" == true || "${clean:-false}" == true ]]; then
  return
fi

# Keep both SDKs in the repo-local installation unless the caller supplies one.
use_installed_dotnet_cli=false
InitializeDotNetCli "$restore"
if [ "$restore" = true ]; then
  ReadGlobalVersion "version"
  InstallDotNetSdk "$_InitializeDotNetCli" "$_ReadGlobalVersion"
fi

export DOTNET_ROOT="$_InitializeDotNetCli"
export DOTNET_HOST_PATH="$_InitializeDotNetCli/dotnet"
ReadGlobalVersion "dotnet"
_InitializeBuildTool="$DOTNET_HOST_PATH"
_InitializeBuildToolCommand="$_InitializeDotNetCli/sdk/$_ReadGlobalVersion/MSBuild.dll"
