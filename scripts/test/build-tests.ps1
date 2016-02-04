#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\common\_common.ps1"

# Publish each test project
loadTestProjectList | foreach {
    #we should use publish to an output path, we will once issue #1183 has been fixed and we can point dotnet test do a dll.
    dotnet build --framework "dnxcore50" --runtime "$Rid" --configuration "$Configuration" "$RepoRoot\test\$($_.ProjectName)"
    if (!$?) {
        Write-Host Command failed: dotnet publish --framework "dnxcore50" --runtime "$Rid" --configuration "$Configuration" "$RepoRoot\test\$($_.ProjectName)"
        exit 1
    }
}
