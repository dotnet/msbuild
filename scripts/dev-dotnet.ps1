#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

$oldPath = $env:PATH
try {
    # Put the stage2 output on the front of the path
    if(!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "You need to have a version of 'dotnet' on your path so we can determine the RID"
    }

    $rid = dotnet --version | where { $_ -match "^ Runtime Id:\s*(.*)$" } | foreach { $matches[1] } 
    $stage2 = "$PSScriptRoot\..\artifacts\$rid\stage2\bin"
    if (Test-Path $stage2) {
        $env:PATH="$stage2;$env:PATH"
    } else {
        Write-Host "You don't have a dev build in the 'artifacts\$rid\stage2' folder!"
    }

    dotnet @args
} finally {
    $env:PATH = $oldPath
}
