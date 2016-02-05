#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

export NUGET_PACKAGES=~/.nuget/packages
export DOTNET_PACKAGES=$NUGET_PACKAGES
export DNX_PACKAGES=$NUGET_PACKAGES

if [ ! -d $NUGET_PACKAGES ]; then
	mkdir -p $NUGET_PACKAGES
fi

if [ -z "$NUGET_PACKAGES_CACHE_TIME_LIMIT" ]; then
	# default the package cache expiration to 1 week, in hours
	export NUGET_PACKAGES_CACHE_TIME_LIMIT=$(( 7 * 24 ))
fi
