#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

#. "$PSScriptRoot\..\common\_common.ps1"

# Get the timestamp of the most recent commit
$timestamp = git log -1 --format=%ct
$commitTime = [timespan]::FromSeconds($timestamp)

$majorVersion = 1
$minorVersion = 0
$buildnumber = 0
$revnumber = $commitTime.TotalSeconds

$VersionSuffix = "dev-$revnumber"

$env:DOTNET_BUILD_VERSION = "$majorVersion.$minorVersion.$buildnumber.$revnumber"