#!/usr/bin/env sh
#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

MANPAGE_TOOL_DIR=$(cd "$(dirname "$0")" || exit; pwd)

docker build -t dotnet-cli-manpage-tool "$MANPAGE_TOOL_DIR"

docker run --volume="$MANPAGE_TOOL_DIR"/..:/manpages dotnet-cli-manpage-tool
