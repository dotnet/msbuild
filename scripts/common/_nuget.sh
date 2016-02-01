#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

## Temporarily redirect to the NuGet package installation location
export NUGET_PACKAGES=~/.nuget/packages
export DOTNET_PACKAGES=$NUGET_PACKAGES
export DNX_PACKAGES=$NUGET_PACKAGES