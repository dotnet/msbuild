#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

# Remove the stage2 output from the path
$splat = $env:PATH.Split(";")
$stripped = @($splat | where { $_ -notlike "*artifacts\win7-x64\stage2*" })
$env:PATH = [string]::Join(";", $stripped)
