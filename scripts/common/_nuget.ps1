#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

if ($env:CI_BUILD -eq "1") {
    $env:NUGET_PACKAGES = (Join-Path $RepoRoot "artifacts\home\.nuget\packages")
} else {
    $env:NUGET_PACKAGES = (Join-Path $env:USERPROFILE ".nuget\packages")
}

$env:DOTNET_PACKAGES = $env:NUGET_PACKAGES
$env:DNX_PACKAGES = $env:NUGET_PACKAGES
if(!(Test-Path $env:NUGET_PACKAGES)) {
    mkdir $env:NUGET_PACKAGES | Out-Null
}