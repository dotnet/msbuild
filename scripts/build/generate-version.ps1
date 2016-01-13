#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

$env:ReleaseSuffix = "dev"
$env:MajorVersion = 1
$env:MinorVersion = 0
$env:PatchVersion = 0

$env:CommitCountVersion = (git rev-list --count HEAD).PadLeft(6, "0")

# Zero Padded Suffix for use with Nuget
$env:VersionSuffix = "$env:ReleaseSuffix-$env:CommitCountVersion"

$env:DOTNET_CLI_VERSION = "$env:MajorVersion.$env:MinorVersion.$env:PatchVersion.$env:CommitCountVersion"