#!/usr/bin/env bash

UNAME=$(uname)
echo "Platform: $UNAME"

if [ "$UNAME" = "Linux" ]; then
    DISTRO=$(cat /etc/os-release | grep "^ID=" | cut -d = -f 2 | sed s/\"//g)
    VERSION=$(cat /etc/os-release | grep "^VERSION_ID=" | cut -d = -f 2 | sed s/\"//g)
    echo "Distro: $DISTRO"
    echo "Version: $VERSION"
    echo "RID: $DISTRO.$VERSION-x64"
else
    VERSION=$(sw_vers -productVersion)
    echo "OS: Mac OS X $VERSION"
    echo "RID: osx.$(echo $VERSION | cut -d . -f 1,2)-x64"
fi
