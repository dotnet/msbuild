// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace Microsoft.Build.UnitTests.Shared;

/// <summary>
/// Windows specific. Class managing system resource - temporary local path mapped to available drive letter.
/// </summary>
public class DummyMappedDrive : IDisposable
{
    public char MappedDriveLetter { get; init; } = 'z';
    private readonly string _mappedPath;
    private readonly bool _mapped;

    public DummyMappedDrive()
    {
        _mappedPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        if (!NativeMethodsShared.IsWindows)
        {
            return;
        }

        Directory.CreateDirectory(_mappedPath);
        File.Create(Path.Combine(_mappedPath, "x")).Dispose();

        for (char driveLetter = 'z'; driveLetter >= 'a'; driveLetter--)
        {
            if (DriveMapping.GetDriveMapping(driveLetter) == string.Empty)
            {
                DriveMapping.MapDrive(driveLetter, _mappedPath);
                MappedDriveLetter = driveLetter;
                _mapped = true;
                return;
            }
        }
    }

    private void ReleaseUnmanagedResources(bool disposing)
    {
        Exception? e = null;
        if (Directory.Exists(_mappedPath))
        {
            try
            {
                Directory.Delete(_mappedPath, true);
            }
            catch (Exception exc)
            {
                e = exc;
                Debug.Fail("Exception in DummyMappedDrive finalizer: " + e.ToString());
            }
        }

        if (_mapped && NativeMethodsShared.IsWindows)
        {
            try
            {
                DriveMapping.UnmapDrive(MappedDriveLetter);
            }
            catch (Exception exc)
            {
                e = e == null ? exc : new AggregateException(e, exc);
                Debug.Fail("Exception in DummyMappedDrive finalizer: " + e.ToString());
            }
        }

        if (disposing && e != null)
        {
            throw e;
        }
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources(true);
        GC.SuppressFinalize(this);
    }

    ~DummyMappedDrive() => ReleaseUnmanagedResources(false);
}
