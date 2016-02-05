#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\common\_common.ps1"

# Publish each test project
loadTestProjectList | foreach {
    #we should use publish to an output path, we will once issue #1183 has been fixed and we can point dotnet test do a dll.
    #we need to add back tfm, rid and configuration, but dotnet test need to be made aware of those as well. Tracked at issue #1237.
    dotnet build "$RepoRoot\test\$($_.ProjectName)"
    if (!$?) {
        Write-Host Command failed: dotnet build "$RepoRoot\test\$($_.ProjectName)"
        exit 1
    }
}
