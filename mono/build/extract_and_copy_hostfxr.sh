#!/usr/bin/env bash

set -e

if [ $# -ne 2 ]; then
	echo "Usage: $0 <dotnet_runtime_nuget_version> <dest_dir>"
	exit 1
fi

DESTDIR=$2

# from `eng/common/tools.sh`

# Enable repos to use a particular version of the on-line dotnet-install scripts.
#    default URL: https://dot.net/v1/dotnet-install.sh
dotnetInstallScriptVersion=${dotnetInstallScriptVersion:-'v1'}

function GetDotNetInstallScript {
  local root=$1
  local install_script="$root/dotnet-install.sh"
  local install_script_url="https://dot.net/$dotnetInstallScriptVersion/dotnet-install.sh"

  if [[ ! -a "$install_script" ]]; then
    mkdir -p "$root"

    echo "Downloading '$install_script_url'"

    # Use curl if available, otherwise use wget
    if command -v curl > /dev/null; then
      curl "$install_script_url" -sSL --retry 10 --create-dirs -o "$install_script"
    else
      wget -q -O "$install_script" "$install_script_url"
    fi
  fi

  # return value
  _GetDotNetInstallScript="$install_script"
}

TMPDIR=`mktemp -d`
DOTNET_DIR=$TMPDIR/.dotnet

OLDCWD=`pwd`
cd $TMPDIR

GetDotNetInstallScript $TMPDIR
sh ./dotnet-install.sh --version $1 --install-dir $DOTNET_DIR --architecture x64 --runtime dotnet --skip-non-versioned-files
find $DOTNET_DIR -name libhostfxr.dylib | xargs -I {} cp -v {} $DESTDIR

cd $OLDCWD
rm -Rf $TMPDIR
