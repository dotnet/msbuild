#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\..\common\_common.ps1"

mkdir -Force $TestPackageDir

loadTestPackageList | foreach {
    dotnet pack "$RepoRoot\test\TestPackages\$($_.ProjectName)" --output "$TestPackageDir"
    
    if (!$?) {
        error "Command failed: dotnet pack"
        Exit 1
    }
}
