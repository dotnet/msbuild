#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

if (!(Get-Command -ErrorAction SilentlyContinue cmake)) {
    throw @"
cmake is required to build the native host 'corehost'
Download it from https://www.cmake.org
"@
}