#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

export RELEASE_SUFFIX=dev
export MAJOR_VERSION=1
export MINOR_VERSION=0
export PATCH_VERSION=0

#TODO @krwq is working on this
export COMMIT_COUNT_VERSION=0

export DOTNET_BUILD_VERSION=$MAJOR_VERSION.$MINOR_VERSION.$PATCH_VERSION.$COMMIT_COUNT_VERSION
