#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

export UNAME=$(uname)

if [ -z "$RID" ]; then
    if [ "$UNAME" == "Darwin" ]; then
        export OSNAME=osx
        if [ -n "$(sw_vers -productVersion | grep 10.10)" ]; then
            export RID=osx.10.10-x64
        elif [ -n "$(sw_vers -productVersion | grep 10.11)" ]; then
            export RID=osx.10.11-x64
        else
            error "unknown OS X: $(sw_vers -productVersion)" 1>&2
        fi
    elif [ "$UNAME" == "Linux" ]; then
        # Detect Distro
        if [ "$(cat /etc/*-release | grep -cim1 ubuntu)" -eq 1 ]; then
            export OSNAME=ubuntu
            export RID=ubuntu.14.04-x64
        elif [ "$(cat /etc/*-release | grep -cim1 centos)" -eq 1 ]; then
            export OSNAME=centos
            export RID=centos.7-x64
        else
            error "unknown Linux Distro" 1>&2
        fi
    else
        error "unknown OS: $UNAME" 1>&2
    fi
fi

if [ -z "$RID" ]; then
    exit 1
fi
