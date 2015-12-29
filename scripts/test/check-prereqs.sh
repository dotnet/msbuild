#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

if ! type -p cmake >/dev/null; then
    error "cmake is required to build the native host 'corehost'"
    error "OS X w/Homebrew: 'brew install cmake'"
    error "Ubuntu: 'sudo apt-get install cmake'"
    exit 1
fi