// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;

namespace Microsoft.Build.UnitTests.Shared;

internal static class DummyMappedDriveUtils
{
    public static string UpdatePathToMappedDrive(string path, char driveLetter)
    {
        const string drivePlaceholder = "%DRIVE%";
        // if this seems to be rooted path - replace with the dummy mount
        if (!string.IsNullOrEmpty(path) && path.StartsWith(drivePlaceholder))
        {
            path = driveLetter + path.Substring(drivePlaceholder.Length);
        }
        return path;
    }

    public static Lazy<DummyMappedDrive?> GetLazyDummyMappedDrive() => new Lazy<DummyMappedDrive?>(() =>
        {
            return NativeMethods.IsWindows ? new DummyMappedDrive() : default;
        });
}
