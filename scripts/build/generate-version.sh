#!/usr/bin/env bash
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# UTC Timestamp of the last commit is used as the build number. This is for easy synchronization of build number between Windows, OSX and Linux builds.
LAST_COMMIT_TIMESTAMP=$(git log -1 --format=%ct)
export DOTNET_BUILD_VERSION=1.0.0-dev-$LAST_COMMIT_TIMESTAMP
echo "Version: $DOTNET_BUILD_VERSION"

unset LAST_COMMIT_TIMESTAMP
