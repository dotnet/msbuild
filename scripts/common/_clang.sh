#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Set up the environment to be used for building with clang.
if which "clang-3.5" > /dev/null 2>&1; then
    export CC="$(which clang-3.5)"
    export CXX="$(which clang++-3.5)"
elif which "clang-3.6" > /dev/null 2>&1; then
    export CC="$(which clang-3.6)"
    export CXX="$(which clang++-3.6)"
elif which clang > /dev/null 2>&1; then
    export CC="$(which clang)"
    export CXX="$(which clang++)"
else
    error "Unable to find Clang Compiler"
    error "Install clang-3.5 or clang3.6"
    exit 1
fi
