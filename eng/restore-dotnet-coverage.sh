#!/usr/bin/env bash

. "$eng_root/common/tools.sh"

InitializeDotNetCli true
dotnet_root=$_InitializeDotNetCli

function InstallGlobalTool {
  local package_name=$1
  local toolpath=$2

  echo "Installing $package_name..."
  echo "You may need to restart your command shell if this is the first dotnet tool you have installed."
  echo $($dotnet_root/dotnet tool install $package_name --prerelease -v $verbosity --tool-path "$toolpath")
}

function InstallGlobalToolWithVersion {
  local package_name=$1
  local toolpath=$2
  local version=$3

  echo "Installing $package_name..."
  echo "You may need to restart your command shell if this is the first dotnet tool you have installed."
  echo $($dotnet_root/dotnet tool install $package_name -v $verbosity --version $version --tool-path "$toolpath")
}

coverageToolsDir=$eng_root/../.tools
dotnetCoverageDir=$coverageToolsDir/dotnet-coverage

export DOTNET_ROOT=$dotnet_root

if [ ! -d "$dotnetCoverageDir" ]; then
  InstallGlobalTool "dotnet-coverage" "$dotnetCoverageDir"
fi
