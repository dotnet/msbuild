#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

$ReleaseSuffix = "dev"
$MajorVersion = 1
$MinorVersion = 0
$PatchVersion = 0

$CommitCountVersion = (git rev-list --count HEAD).PadLeft(6, "0")

# Zero Padded Suffix for use with Nuget
$VersionSuffix = "$ReleaseSuffix-$CommitCountVersion"

$env:DOTNET_BUILD_VERSION = "$MajorVersion.$MinorVersion.$PatchVersion.$CommitCountVersion"