#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

set -e

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ "$SOURCE" != /* ]] && SOURCE="$DIR/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
DIR="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

source "$DIR/../common/_common.sh"

say() {
    printf "%b\n" "dotnet_install_dnx: $1"
}

doInstall=true

DNX_FEED="https://api.nuget.org/packages"
DNX_PACKAGE_VERSION="1.0.0-rc1-update1"
DNX_VERSION="1.0.0-rc1-16231"

if [ "$OSNAME" == "osx" ]; then
    DNX_FLAVOR="dnx-coreclr-darwin-x64"
elif [ "$OSNAME" == "ubuntu" ]; then
    DNX_FLAVOR="dnx-coreclr-linux-x64"
elif [ "$OSNAME" == "centos"  ]; then
    # No support dnx on redhat yet.
    # using patched dnx
    DNX_FEED="https://dotnetcli.blob.core.windows.net/dotnet/redhat_dnx"
    DNX_PACKAGE_VERSION="1.0.0-rc2-15000"
    DNX_VERSION="1.0.0-rc2-15000"
    DNX_FLAVOR="dnx-coreclr-redhat-x64"
else
    error "unknown OS: $OSNAME" 1>&2
    exit 1
fi    

DNX_URL="$DNX_FEED/$DNX_FLAVOR.$DNX_PACKAGE_VERSION.nupkg"

say "Preparing to install DNX to $DNX_DIR"
say "Requested Version: $DNX_VERSION"

if [ -e "$DNX_ROOT/dnx" ] ; then
    dnxOut=`$DNX_ROOT/dnx --version | grep '^ Version: ' | awk '{ print $2; }'`
    
    say "Local Version: $dnxOut"
    
    if [ $dnxOut =  $DNX_VERSION ] ; then
        say "You already have the requested version."
        
        doInstall=false
    fi
else
    say "Local Version: Not Installed"
fi

if [ $doInstall = true ] ; then
    rm -rf $DNX_DIR

    mkdir -p $DNX_DIR
    curl -o $DNX_DIR/dnx.zip $DNX_URL --silent
    unzip -qq $DNX_DIR/dnx.zip -d $DNX_DIR
    chmod a+x $DNX_ROOT/dnu $DNX_ROOT/dnx   
fi