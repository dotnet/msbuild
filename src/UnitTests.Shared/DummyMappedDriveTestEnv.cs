// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.UnitTests.Shared
{
    public class DummyMappedDriveTestEnv : IDisposable
    {
        public DummyMappedDrive? _mappedDrive;

        public DummyMappedDriveTestEnv()
        {
            if (NativeMethods.IsWindows)
            {
                // let's create the mapped drive only once it's needed by any test, then let's reuse;
                _mappedDrive ??= new DummyMappedDrive();
            }
        }

        public string UpdatePathToMappedDrive(string path)
        {
            const string drivePlaceholder = "%DRIVE%";
            // if this seems to be rooted path - replace with the dummy mount
            if (!string.IsNullOrEmpty(path) && path.StartsWith(drivePlaceholder) && _mappedDrive != null)
            {
                path = _mappedDrive.MappedDriveLetter + path.Substring(drivePlaceholder.Length);
            }
            return path;
        }

        public void Dispose()
        {
            if (_mappedDrive != null)
            {
                _mappedDrive.Dispose();
            }
        }
    }
}
