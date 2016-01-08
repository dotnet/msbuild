#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

params=("$@")

for i in "${!params[@]}"
do
    lowerI="$(echo ${params[$i]} | awk '{print tolower($0)}')"
    case $lowerI in
    "release" | "-release")
        export CONFIGURATION=Release
        ;;
    "debug" | "-debug")
        export CONFIGURATION=Debug
        ;;
    "offline" | "-offline")
        export OFFLINE=true
        ;;
    "nopackage" | "-nopackage")
        export NOPACKAGE=true
        ;;
    "nocache" | "-nocache")
        export NOCACHE=--No-Cache
        ;;
    "--buildindocker")
        export BUILD_IN_DOCKER=true
        export DOCKER_OS=${params[i+1]}
        ;;
    *)
    esac
done
