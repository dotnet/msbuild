// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    ///  Covers <see cref="WindowsNative"/>'s migration from `[DllImport]`-based
    ///  <c>FindFirstFileW</c> / <c>FindNextFileW</c> / <c>FindClose</c> / <c>PathMatchSpecExW</c>
    ///  to CsWin32 (<c>PInvoke.FindFirstFile</c> / <c>FindNextFile</c> / <c>FindClose</c> /
    ///  <c>PathMatchSpecEx</c>), plus the <c>Win32FindData</c> adapter built from the
    ///  blittable <c>WIN32_FIND_DATAW</c>.
    /// </summary>
    [SupportedOSPlatform("windows6.0.6000")]
    public sealed class WindowsNative_Tests : IDisposable
    {
        private readonly string _tempDir;

        public WindowsNative_Tests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "msb-WindowsNative_Tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
            }
            catch
            {
                // Best-effort: leave it to the temp folder cleanup if something locks a file.
            }
        }

        [WindowsOnlyFact]
        public void FindFirstFile_ReturnsExpectedFileName()
        {
            // Single concrete file looked up by its exact name. CFileName must come back
            // through the WIN32_FIND_DATAW.cFileName fixed buffer -> managed string adapter.
            string filePath = Path.Combine(_tempDir, "alpha.txt");
            File.WriteAllText(filePath, "test");

            using SafeFindFileHandle handle = WindowsNative.FindFirstFileW(filePath, out WindowsNative.Win32FindData data);
            handle.IsInvalid.ShouldBeFalse();
            data.CFileName.ShouldBe("alpha.txt");
        }

        [WindowsOnlyFact]
        public void FindNextFile_EnumeratesAllEntries()
        {
            // FindFirst + repeated FindNext should walk every file in the directory exactly once.
            // "." and ".." come back too — exclude them, then compare to the canonical
            // Directory.GetFiles enumeration of the same directory.
            string[] names = ["a.txt", "b.txt", "c.txt"];
            foreach (string n in names)
            {
                File.WriteAllText(Path.Combine(_tempDir, n), n);
            }

            using SafeFindFileHandle handle = WindowsNative.FindFirstFileW(Path.Combine(_tempDir, "*"), out WindowsNative.Win32FindData data);
            handle.IsInvalid.ShouldBeFalse();

            var found = new System.Collections.Generic.List<string>();
            do
            {
                if (data.CFileName is not "." and not "..")
                {
                    found.Add(data.CFileName);
                }
            }
            while (WindowsNative.FindNextFileW(handle, out data));

            found.OrderBy(s => s).ShouldBe(names.OrderBy(s => s));
        }

        [WindowsOnlyFact]
        public void Win32FindData_AttributesAndTimes_MatchManagedAPIs()
        {
            // The adapter must preserve every field the callers depend on. Round-trip the
            // attributes and last-write time against the standard managed file-system APIs.
            string filePath = Path.Combine(_tempDir, "props.txt");
            File.WriteAllText(filePath, "x");
            File.SetAttributes(filePath, FileAttributes.ReadOnly | FileAttributes.Normal);

            using SafeFindFileHandle handle = WindowsNative.FindFirstFileW(filePath, out WindowsNative.Win32FindData data);
            handle.IsInvalid.ShouldBeFalse();

            File.GetAttributes(filePath).ShouldBe(data.DwFileAttributes);

            long fileTimeFromFindData =
                ((long)(uint)data.FtLastWriteTime.dwHighDateTime << 32)
                | (uint)data.FtLastWriteTime.dwLowDateTime;
            DateTime.FromFileTimeUtc(fileTimeFromFindData)
                .ShouldBe(File.GetLastWriteTimeUtc(filePath), TimeSpan.FromMilliseconds(10));

            // nFileSizeHigh:nFileSizeLow is the 64-bit size.
            ((long)data.NFileSizeHigh << 32 | data.NFileSizeLow).ShouldBe(1L);
        }

        [WindowsOnlyFact]
        public void FindFirstFile_NonExistentDirectory_ReturnsInvalidHandle()
        {
            // Bad path must surface as a SafeHandle whose IsInvalid is true (FindFirstFile
            // returns INVALID_HANDLE_VALUE in this case). The SafeHandle must not throw on
            // Dispose for an invalid handle either.
            string bogus = Path.Combine(_tempDir, "does-not-exist", "*");
            using SafeFindFileHandle handle = WindowsNative.FindFirstFileW(bogus, out _);
            handle.IsInvalid.ShouldBeTrue();
        }

        [WindowsOnlyFact]
        public void PathMatchSpecExW_MatchAndNonMatch()
        {
            // Win32 PathMatchSpecEx returns S_OK (ErrorSuccess) on a match and S_FALSE
            // on a non-match. The wrapper preserves that as the raw int.
            WindowsNative.PathMatchSpecExW("foo.txt", "*.txt", WindowsNative.DwFlags.PmsfNormal)
                .ShouldBe(WindowsNative.ErrorSuccess);
            WindowsNative.PathMatchSpecExW("foo.bin", "*.txt", WindowsNative.DwFlags.PmsfNormal)
                .ShouldNotBe(WindowsNative.ErrorSuccess);
        }

        [WindowsOnlyFact]
        public void SafeFindFileHandle_Dispose_IsIdempotent()
        {
            // SafeFindFileHandle's ReleaseHandle calls PInvoke.FindClose. Double-dispose and
            // implicit-dispose at scope exit must both be no-ops.
            string filePath = Path.Combine(_tempDir, "z.txt");
            File.WriteAllText(filePath, "z");

            SafeFindFileHandle handle = WindowsNative.FindFirstFileW(filePath, out _);
            handle.IsInvalid.ShouldBeFalse();

            handle.Dispose();
            Should.NotThrow(() => handle.Dispose());
            handle.IsClosed.ShouldBeTrue();
        }
    }
}
