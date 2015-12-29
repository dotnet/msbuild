#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

for i in "$@"
do
    lowerI="$(echo $i | awk '{print tolower($0)}')"
    case $lowerI in
    release)
        export CONFIGURATION=Release
        ;;
    debug)
        export CONFIGURATION=Debug
        ;;
    offline)
        export OFFLINE=true
        ;;
    nopackage)
        export NOPACKAGE=true
        ;;
    *)
    esac
done