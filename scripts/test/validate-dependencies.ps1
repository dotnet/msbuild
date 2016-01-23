#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

. "$PSScriptRoot\..\common\_common.ps1"

# Run Validation for Project.json dependencies
dotnet publish $RepoRoot\tools\MultiProjectValidator -o $Stage2Dir\..\tools -c "$Configuration"

$pjvalidatePath = "$Stage2Dir\..\tools\$Configuration\$Tfm"
if (! (Test-Path $pjvalidatePath)) {
	$pjvalidatePath = "$Stage2Dir\..\tools"
}

& "$pjvalidatePath\pjvalidate" "$RepoRoot\src"
# TODO For release builds, this should be uncommented and fail.
# if (!$?) {
#     Write-Host "Project Validation Failed"
#     Exit 1
# }