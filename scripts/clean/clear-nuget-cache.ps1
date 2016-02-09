#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. $PSScriptRoot\..\common\_common.ps1

if ($env:CI_BUILD -eq "1") {
    # periodically clear out the package cache on the CI server
    $PackageCacheFile = "$env:NUGET_PACKAGES\packageCacheTime.txt"

    if(!(Test-Path $PackageCacheFile)) {
        Get-Date | Out-File -FilePath $PackageCacheFile
    }
    else {
        $PackageCacheTimeProperty = Get-ItemProperty -Path $PackageCacheFile -Name CreationTimeUtc
        $PackageCacheTime = [datetime]($PackageCacheTimeProperty).CreationTimeUtc

        if ($PackageCacheTime -lt ([datetime]::UtcNow).AddHours(-$env:NUGET_PACKAGES_CACHE_TIME_LIMIT)) {
            header "Clearing package cache"

            Remove-Item -Recurse -Force "$env:NUGET_PACKAGES"
            mkdir $env:NUGET_PACKAGES | Out-Null
            Get-Date | Out-File -FilePath $PackageCacheFile
        }
    }
}
