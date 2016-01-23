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
    "release" | "--release")
        export CONFIGURATION=Release
        ;;
    "debug" | "--debug")
        export CONFIGURATION=Debug
        ;;
    "offline" | "--offline")
        export OFFLINE=true
        ;;
    "nopackage" | "--nopackage")
        export NOPACKAGE=true
        ;;
    "--buildindocker-ubuntu")
        export BUILD_IN_DOCKER=true
        export DOCKER_OS=ubuntu
        ;;
    "--buildindocker-centos")
        export BUILD_IN_DOCKER=true
        export DOCKER_OS=centos
        ;;
    *)
    esac
done
