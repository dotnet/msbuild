// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests;

public class DirectoryCaseSensitivity_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void DetectionMatchesActualLookupBehavior()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder folder = env.CreateFolder(createFolder: true);
        folder.CreateFile("Probe.txt", string.Empty);

        bool expected = !File.Exists(Path.Combine(folder.Path, "pROBE.TXT"));

        DirectoryCaseSensitivity.ProbeByLookup(folder.Path).ShouldBe(expected);
        DirectoryCaseSensitivity.QueryFileSystem(folder.Path)?.ShouldBe(expected);
        DirectoryCaseSensitivity.IsCaseSensitive(folder.Path).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Foo.txt", null)]
    [InlineData("Foo.txt", "fOO.TXT")] // The flipped name exists as a separate entry.
    public void DetectsCaseSensitiveDirectory(string fileName, string? flippedFileName)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder folder = env.CreateCaseSensitiveFolder();
        folder.CreateFile(fileName, string.Empty);
        if (flippedFileName is not null)
        {
            folder.CreateFile(flippedFileName, string.Empty);
        }

        Directory.GetFiles(folder.Path).Length.ShouldBe(flippedFileName is null ? 1 : 2);
        DirectoryCaseSensitivity.ProbeByLookup(folder.Path).ShouldBe(true);

        // Windows and macOS have an API for this; other platforms rely on the probe.
        bool? expectedFromFileSystem = NativeMethods.IsWindows || NativeMethods.IsOSX ? true : null;
        DirectoryCaseSensitivity.QueryFileSystem(folder.Path).ShouldBe(expectedFromFileSystem);
        DirectoryCaseSensitivity.IsCaseSensitive(folder.Path).ShouldBeTrue();
    }

    [Fact]
    public void ProbeIsUndeterminedWithoutUsableEntries()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        TransientTestFolder folder = env.CreateFolder(createFolder: true);

        DirectoryCaseSensitivity.ProbeByLookup(folder.Path).ShouldBeNull();

        folder.CreateFile("123.456", string.Empty);
        DirectoryCaseSensitivity.ProbeByLookup(folder.Path).ShouldBeNull();
    }

    [Fact]
    public void MissingDirectoryFallsBackToOperatingSystemDefaultWithoutCaching()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string directory = Path.Combine(env.CreateFolder(createFolder: true).Path, "Missing");

        DirectoryCaseSensitivity.Detect(directory).ShouldBeNull();
        DirectoryCaseSensitivity.IsCaseSensitive(directory).ShouldBe(FileUtilities.IsFileSystemCaseSensitive);

        // The fallback is not cached, so the directory is detected once it exists.
        TransientTestFolder caseSensitiveFolder = env.CreateCaseSensitiveFolder();
        caseSensitiveFolder.CreateFile("Foo.txt", string.Empty);
        Directory.Move(caseSensitiveFolder.Path, directory);

        DirectoryCaseSensitivity.IsCaseSensitive(directory).ShouldBeTrue();
    }
}
