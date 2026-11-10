// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.FileSystem;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.OM;

internal sealed class DriveEnumeratingWildcardFileSystem : MSBuildFileSystemBase
{
    private const string FixtureDirectory = "msbuild-root-warning-fixture";
    private readonly IFileSystem _physicalFileSystem = FileSystems.Default;
    private int _enumerations;
    private int _rootEnumerations;
    private int _returnedFiles;
    private int _unexpectedEnumerations;

    internal static DriveEnumeratingWildcardFileSystem CreateInChild(TestEnvironment environment, string parentProcessId)
        => environment.WithTransientTestState(new ChildFileSystemOverride(parentProcessId)).FileSystem;

    internal void AssertRootEnumerationWasBounded(ITestOutputHelper output)
    {
        _enumerations.ShouldBeInRange(1, 128);
        _rootEnumerations.ShouldBeGreaterThan(0);
        _returnedFiles.ShouldBeGreaterThan(0);
        _unexpectedEnumerations.ShouldBe(0);
        output.WriteLine($"Hermetic root enumeration: {_rootEnumerations} virtual root requests, {_enumerations} total requests, {_returnedFiles} returned files; no physical enumeration delegate.");
    }

    public override IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Enumerate(path, searchPattern, searchOption, files: true, directories: false);

    public override IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Enumerate(path, searchPattern, searchOption, files: false, directories: true);

    public override IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Enumerate(path, searchPattern, searchOption, files: true, directories: true);

    public override FileAttributes GetAttributes(string path)
        => IsVirtualFile(path) ? FileAttributes.Normal
            : IsVirtualDirectory(path) ? FileAttributes.Directory
            : _physicalFileSystem.GetAttributes(path);

    public override DateTime GetLastWriteTimeUtc(string path)
        => IsVirtualFile(path) || IsVirtualDirectory(path)
            ? new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            : _physicalFileSystem.GetLastWriteTimeUtc(path);

    public override bool DirectoryExists(string path)
        => IsVirtualDirectory(path) || _physicalFileSystem.DirectoryExists(path);

    public override bool FileExists(string path)
        => IsVirtualFile(path) || _physicalFileSystem.FileExists(path);

    public override bool FileOrDirectoryExists(string path)
        => IsVirtualFile(path) || IsVirtualDirectory(path) || _physicalFileSystem.FileOrDirectoryExists(path);

    private IEnumerable<string> Enumerate(string path, string pattern, SearchOption option, bool files, bool directories)
    {
        string fullPath = Normalize(path);
        string root = Path.GetPathRoot(fullPath)!;
        if (Interlocked.Increment(ref _enumerations) > 128 || !IsVirtualDirectory(fullPath))
        {
            Interlocked.Increment(ref _unexpectedEnumerations);
            throw new InvalidOperationException($"Root-warning fixture attempted enumeration outside its bounded virtual tree: {path}");
        }

        if (FileUtilities.PathComparer.Equals(fullPath, root))
        {
            Interlocked.Increment(ref _rootEnumerations);
        }

        IEnumerable<string> entries = (files, directories) switch
        {
            (true, true) => GetFiles(root).Concat(GetDirectories(root).Skip(1)),
            (true, false) => GetFiles(root),
            (false, true) => GetDirectories(root).Skip(1),
            _ => throw new InvalidOperationException("No entry type requested.")
        };

        foreach (string entry in entries)
        {
            bool withinDirectory = option == SearchOption.AllDirectories
                ? entry.StartsWith(fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? fullPath : fullPath + Path.DirectorySeparatorChar, FileUtilities.PathComparison)
                : FileUtilities.PathComparer.Equals(Path.GetDirectoryName(entry), fullPath);

            if (withinDirectory && FileMatcher.IsFileNameMatch(entry, pattern))
            {
                if (IsVirtualFile(entry))
                {
                    Interlocked.Increment(ref _returnedFiles);
                }

                yield return entry;
            }
        }
    }

    private static bool IsVirtualFile(string path)
    {
        string fullPath = Normalize(path);
        return GetFiles(Path.GetPathRoot(fullPath)!).Contains(fullPath, FileUtilities.PathComparer);
    }

    private static bool IsVirtualDirectory(string path)
    {
        string fullPath = Normalize(path);
        return GetDirectories(Path.GetPathRoot(fullPath)!).Contains(fullPath, FileUtilities.PathComparer);
    }

    private static string Normalize(string path)
    {
        string fullPath = Path.GetFullPath(FileUtilities.FixFilePath(path));
        string root = Path.GetPathRoot(fullPath)!;
        return fullPath.Length == root.Length ? root : fullPath.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static string[] GetDirectories(string root) =>
    [
        root,
        Path.Combine(root, FixtureDirectory),
        Path.Combine(root, FixtureDirectory, "nested"),
        Path.Combine(root, "Certificates")
    ];

    private static string[] GetFiles(string root) =>
    [
        Path.Combine(root, "root.log"),
        Path.Combine(root, "root.cs"),
        Path.Combine(root, FixtureDirectory, "fixture.log"),
        Path.Combine(root, FixtureDirectory, "fixture.cs"),
        Path.Combine(root, FixtureDirectory, "fixture.targets"),
        Path.Combine(root, FixtureDirectory, "nested", "nested.log"),
        Path.Combine(root, "Certificates", "certificate.pdb")
    ];

    private sealed class ChildFileSystemOverride : TransientTestState
    {
        private readonly FileMatcher _previousMatcher;
        private readonly MSBuildTestPipelineStartup _startup;

        internal DriveEnumeratingWildcardFileSystem FileSystem { get; }

        internal ChildFileSystemOverride(string parentProcessId)
        {
            using Process current = Process.GetCurrentProcess();
            current.Id.ShouldNotBe(int.Parse(parentProcessId, CultureInfo.InvariantCulture));

            // RemoteExecutor invokes the delegate directly, without xUnit's assembly startup.
            _startup = new MSBuildTestPipelineStartup();
            _previousMatcher = FileMatcher.Default;
            FileSystem = new DriveEnumeratingWildcardFileSystem();
            FileMatcher.Default = new FileMatcher(FileSystem);
        }

        public override void Revert()
        {
            FileMatcher.Default = _previousMatcher;
            _startup.StopAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
