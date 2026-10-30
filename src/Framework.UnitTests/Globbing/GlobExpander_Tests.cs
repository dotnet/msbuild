// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Globbing;

public sealed class GlobExpander_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Theory]
    [InlineData("Assets/**;**/*.props;**/*.targets")]
    [InlineData("*.props;**/*.targets")]
    [InlineData("Assets/**/theme.props;**/*.props;**/generated/**/*.targets")]
    [InlineData("missing/**/*.props;invalid.../**/*.props;**/*.targets")]
    [InlineData("Assets/;**/*.props;**/*.targets")]
    [InlineData("literal.props;**/*.props;**/*.targets")]
    [InlineData("**/*.;**/*.*")]
    [InlineData("./**/*.props;**/*.props;**/*.targets")]
    [InlineData("Assets/../**/*.props;**/*.targets")]
    [InlineData("./**/*.props;./**/*.targets")]
    public void MatchesIndividualExpansionForEveryFragment(string include)
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateTree(env);
        string[] specifications = include.Split(';').Select(Normalize).ToArray();
        List<string> excludes = [Normalize("obj/**"), Normalize("**/*.tmp")];
        FileMatcher reference = new(FileSystems.Default, new ConcurrentDictionary<string, IReadOnlyList<string>>());
        FileMatcher matcher = new(FileSystems.Default, new ConcurrentDictionary<string, IReadOnlyList<string>>());
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, excludes);

        foreach (string specification in specifications)
        {
            string[] expected = reference.GetFiles(root, specification, excludes).FileList;
            string[] actual = matcher.GetFiles(root, specification, excludes, expander).FileList;

            actual.OrderBy(path => path, StringComparer.Ordinal).ShouldBe(
                expected.OrderBy(path => path, StringComparer.Ordinal));
        }
    }

    [Fact]
    public void MixedAbsoluteAndRelativePatternsKeepTheirOutputForms()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateTree(env);
        string[] specifications = [Path.Combine(root, Normalize("Assets/**")), Normalize("**/*.props")];
        FileMatcher reference = new(FileSystems.Default, new ConcurrentDictionary<string, IReadOnlyList<string>>());
        FileMatcher matcher = new(FileSystems.Default, new ConcurrentDictionary<string, IReadOnlyList<string>>());
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, null);

        foreach (string specification in specifications)
        {
            matcher.GetFiles(root, specification, globExpander: expander).FileList.OrderBy(path => path, StringComparer.Ordinal).ShouldBe(
                reference.GetFiles(root, specification).FileList.OrderBy(path => path, StringComparer.Ordinal));
        }
    }

    [Fact]
    public void NewlineAncestorPreservesLegacyMatchingAndCachedResults()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string normal = Path.Combine(root, "normal");
        string normalSubdirectory = Path.Combine(normal, "sub");
        // Model Unix names without validating or creating them on Windows.
        string newline = root + Path.DirectorySeparatorChar + "line\nbreak";
        string newlineSubdirectory = newline + Path.DirectorySeparatorChar + "sub";
        string[] directories =
        [
            normal, newline,
            .. Enumerable.Range(0, Math.Max(1, NativeMethods.GetLogicalCoreCount() / 2))
                .Select(index => Path.Combine(root, $"empty{index}"))
        ];
        string[] specifications = [Normalize("**/sub/*.cs"), Normalize("**/sub/*.targets")];

        IReadOnlyList<string> GetEntries(FileMatcher.FileSystemEntity type, string path, string pattern, string projectDirectory, bool stripProjectDirectory)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            if (type == FileMatcher.FileSystemEntity.Directories)
            {
                return path == root ? directories
                    : path == normal ? [normalSubdirectory]
                    : path == newline ? [newlineSubdirectory]
                    : [];
            }

            return path == normalSubdirectory
                ? [Path.Combine(path, "source.cs"), Path.Combine(path, "build.targets")]
                : path == newlineSubdirectory ? [path + Path.DirectorySeparatorChar + "source.cs"]
                : [];
        }

        FileMatcher reference = new(FileSystems.Default, GetEntries, new(), allowDirectEnumeration: true);
        FileMatcher matcher = new(FileSystems.Default, GetEntries, new(), allowDirectEnumeration: true);
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, null);
        foreach (string glob in specifications)
        {
            string[] expected = reference.GetFiles(root, glob).FileList;
            expected.ShouldHaveSingleItem();
            string[] actual = matcher.GetFiles(root, glob, globExpander: expander).FileList;

            actual.OrderBy(path => path, StringComparer.Ordinal).ShouldBe(
                expected.OrderBy(path => path, StringComparer.Ordinal));
            matcher.GetFiles(root, glob).FileList.ShouldBe(actual);
        }
    }

    [Fact]
    public void CaseDistinctFixedRootsDoNotCrossMatchOrContaminateTheCache()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string upper = env.CreateFolder(Path.Combine(root, "A")).Path;
        string lower = Path.Combine(root, "a");
        string[] specifications = [Normalize("A/**"), Normalize("**/*.props")];

        // Model case-sensitive listings independently of the host filesystem.
        IReadOnlyList<string> GetEntries(FileMatcher.FileSystemEntity type, string path, string pattern, string projectDirectory, bool stripProjectDirectory)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            return type == FileMatcher.FileSystemEntity.Directories
                ? path == root ? [upper, lower] : []
                : path == upper ? [Path.Combine(upper, "one.txt")]
                : path == lower ? [Path.Combine(lower, "two.props")]
                : [];
        }

        FileMatcher reference = new(FileSystems.Default, GetEntries, new(), allowDirectEnumeration: true);
        FileMatcher matcher = new(FileSystems.Default, GetEntries, new(), allowDirectEnumeration: true);
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, null);
        foreach (string specification in specifications)
        {
            matcher.GetFiles(root, specification, globExpander: expander).FileList.ShouldBe(
                reference.GetFiles(root, specification).FileList);
        }

        matcher.GetFiles(root, specifications[0]).FileList.ShouldBe([Normalize("A/one.txt")]);
    }

    [Fact]
    public void UnreachedCaseAliasFallsBackAndPreservesItsLexicalPrefix()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.CreateFolder(Path.Combine(root, "ASSETS"));
        string listedDirectory = Path.Combine(root, "Assets");
        string[] specifications = [Normalize("ASSETS/**"), Normalize("**/*.props")];

        IReadOnlyList<string> GetEntries(FileMatcher.FileSystemEntity type, string path, string pattern, string projectDirectory, bool stripProjectDirectory)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            return type == FileMatcher.FileSystemEntity.Directories
                ? path == root ? [listedDirectory] : []
                : path.Equals(listedDirectory, StringComparison.OrdinalIgnoreCase)
                    ? [Path.Combine(path, "theme.props")]
                    : [];
        }

        FileMatcher reference = new(FileSystems.Default, GetEntries, new(), allowDirectEnumeration: true);
        FileMatcher matcher = new(FileSystems.Default, GetEntries, new(), allowDirectEnumeration: true);
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, null);
        foreach (string specification in specifications)
        {
            matcher.GetFiles(root, specification, globExpander: expander).FileList.ShouldBe(
                reference.GetFiles(root, specification).FileList);
        }

        matcher.GetFiles(root, specifications[0]).FileList.ShouldBe([Normalize("ASSETS/theme.props")]);
    }

    [Fact]
    public void ShallowGlobsStartingAtASeparatorRootShareTraversalWithoutLosingMatches()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string projectDirectory = Path.GetPathRoot(env.CreateFolder().Path)!;
        string root = Path.DirectorySeparatorChar.ToString();
        string child = Path.Combine(root, "glob-test");
        ConcurrentDictionary<string, CountingEntries> entries = new();
        string[] specifications = [Path.Combine(root, "*", "*.props"), Path.Combine(root, "*", "*.targets")];
        FileMatcher matcher = new(
            FileSystems.Default,
            (type, path, _, _, _) =>
            {
                string[] paths = type == FileMatcher.FileSystemEntity.Directories
                    ? path == root ? [child] : []
                    : path == child ? [Path.Combine(child, "one.props"), Path.Combine(child, "two.targets")] : [];
                CountingEntries result = new(paths);
                entries[type + ";" + path] = result;
                return result;
            },
            new(),
            allowDirectEnumeration: true);
        FileMatcher.GlobExpander expander = new(matcher, projectDirectory, specifications, null);

        matcher.GetFiles(projectDirectory, specifications[0], globExpander: expander).FileList.ShouldBe(
            [Path.Combine(child, "one.props")]);
        matcher.GetFiles(projectDirectory, specifications[1], globExpander: expander).FileList.ShouldBe(
            [Path.Combine(child, "two.targets")]);
        entries.Values.ShouldAllBe(entry => entry.EnumerationCount == 1);
    }

    [ConditionalFact(typeof(GlobExpander_Tests), nameof(IsCaseSensitiveFileSystem))]
    public void PhysicalCaseDistinctRootsMatchIndependentExpansion()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string upper = env.CreateFolder(Path.Combine(root, "A")).Path;
        string lower = env.CreateFolder(Path.Combine(root, "a")).Path;
        File.WriteAllText(Path.Combine(upper, "one.txt"), string.Empty);
        File.WriteAllText(Path.Combine(lower, "two.props"), string.Empty);
        string[] specifications = [Normalize("A/**"), Normalize("**/*.props")];
        FileMatcher matcher = new(FileSystems.Default, fileEntryExpansionCache: new());
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, null);

        matcher.GetFiles(root, specifications[0], globExpander: expander).FileList.ShouldBe([Normalize("A/one.txt")]);
        matcher.GetFiles(root, specifications[1], globExpander: expander).FileList.ShouldBe([Normalize("a/two.props")]);
    }

    public static bool IsCaseSensitiveFileSystem => FileUtilities.PathComparison == StringComparison.Ordinal;

    [ConditionalFact(typeof(GlobExpander_Tests), nameof(SupportsParallelGlobbing))]
    public void CaseCollidingResultsPreserveIndependentEngineSortOrder()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        env.CreateFolder(Path.Combine(root, "obj"));
        int degree = NativeMethods.GetLogicalCoreCount() / 2;
        string[] directories = Enumerable.Range(0, degree + 1)
            .Select(index => Path.Combine(root, index == 1 ? "A" : index == degree ? "a" : $"empty{index}"))
            .ToArray();
        FileMatcher.GetFileSystemEntries getEntries = (type, path, _, _, _) =>
            type == FileMatcher.FileSystemEntity.Directories
                ? path.TrimEnd(Path.DirectorySeparatorChar) == root ? directories : []
                : path == directories[1] || path == directories[degree] ? [Path.Combine(path, "x.props")] : [];
        FileMatcher reference = new(FileSystems.Default, getEntries, new(), allowDirectEnumeration: true);
        FileMatcher matcher = new(FileSystems.Default, getEntries, new(), allowDirectEnumeration: true);
        string[] specifications = [Normalize("**/*.props"), Normalize("**/*.targets")];
        List<string> excludes = [Normalize("obj/**")];
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, excludes);

        string[] expected = reference.GetFiles(root, specifications[0], excludes).FileList;
        string[] actual = matcher.GetFiles(root, specifications[0], excludes, expander).FileList;
        Array.Sort(expected, StringComparer.OrdinalIgnoreCase);
        Array.Sort(actual, StringComparer.OrdinalIgnoreCase);

        expected.ShouldBe([Normalize("A/x.props"), Normalize("a/x.props")]);
        actual.ShouldBe(expected);
    }

    public static bool SupportsParallelGlobbing => NativeMethods.GetLogicalCoreCount() >= 4;

    [Fact]
    public void FixedRootHiddenFromAncestorListingFallsBackIndependently()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string parent = env.CreateFolder(Path.Combine(root, "hidden")).Path;
        string child = env.CreateFolder(Path.Combine(parent, "sub")).Path;
        File.WriteAllText(Path.Combine(child, "item.props"), string.Empty);
        FileMatcher.GetFileSystemEntries getEntries = (type, path, _, _, _) =>
            type == FileMatcher.FileSystemEntity.Directories
                ? path.TrimEnd(Path.DirectorySeparatorChar) == parent ? [] : Directory.GetDirectories(path)
                : Directory.GetFiles(path);
        FileMatcher reference = new(FileSystems.Default, getEntries, new(), allowDirectEnumeration: true);
        FileMatcher matcher = new(FileSystems.Default, getEntries, new(), allowDirectEnumeration: true);
        string[] specifications = [Normalize("hidden/sub/**"), Normalize("**/*.props")];
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, null);

        foreach (string specification in specifications)
        {
            matcher.GetFiles(root, specification, globExpander: expander).FileList.ShouldBe(
                reference.GetFiles(root, specification).FileList);
        }

        matcher.GetFiles(root, specifications[0]).FileList.ShouldBe([Normalize("hidden/sub/item.props")]);
    }

#if FEATURE_SYMLINK_TARGET
    [RequiresSymbolicLinksFact]
    public void FixedRootBelowSkippedAncestorLinkFallsBackIndependently()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = env.CreateFolder().Path;
        string subdirectory = env.CreateFolder(Path.Combine(root, "sub")).Path;
        File.WriteAllText(Path.Combine(subdirectory, "item.props"), string.Empty);
        string link = env.CreateFolder(Path.Combine(root, "link"), createFolder: false).Path;
        Directory.CreateSymbolicLink(link, root);
        string[] specifications = [Normalize("link/sub/**"), Normalize("**/*.props")];
        FileMatcher reference = new(FileSystems.Default, fileEntryExpansionCache: new());
        FileMatcher matcher = new(FileSystems.Default, fileEntryExpansionCache: new());
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, null);

        foreach (string specification in specifications)
        {
            matcher.GetFiles(root, specification, globExpander: expander).FileList.ShouldBe(
                reference.GetFiles(root, specification).FileList);
        }

        matcher.GetFiles(root, specifications[0]).FileList.ShouldBe([Normalize("link/sub/item.props")]);
    }
#endif

    [Fact]
    public void SharesLogicalTraversalAndReturnsIndependentCachedArrays()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateTree(env);
        ConcurrentDictionary<string, CountingEntries> entries = new();
        FileMatcher matcher = CreateRecordingMatcher(entries);
        string[] specifications = [Normalize("Assets/**"), Normalize("**/*.props"), Normalize("**/*.targets")];
        List<string> excludes = [Normalize("obj/**")];
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, excludes);
        ConcurrentDictionary<string, CountingEntries> referenceEntries = new();
        FileMatcher reference = CreateRecordingMatcher(referenceEntries);
        foreach (string specification in specifications)
        {
            reference.GetFiles(root, specification, excludes);
        }

        referenceEntries.Values.ShouldContain(entry => entry.EnumerationCount > 1);

        string[] assets = matcher.GetFiles(root, specifications[0], excludes, expander).FileList;
        assets.ShouldContain(Normalize("Assets/styles/theme.props"));
        matcher.GetFiles(root, specifications[1], excludes, expander).FileList.ShouldContain(Normalize("Assets/styles/theme.props"));
        matcher.GetFiles(root, specifications[2], excludes, expander).FileList.ShouldContain(Normalize("build/Common.targets"));
        entries.Values.ShouldAllBe(entry => entry.EnumerationCount == 1);
        entries.Keys.ShouldNotContain("Files;" + Path.Combine(root, "obj"));

        assets[0] = "caller mutation";
        FileMatcher.GlobExpander warm = new(matcher, root, specifications, excludes);
        matcher.GetFiles(root, specifications[0], excludes, warm).FileList.ShouldNotContain("caller mutation");
        entries.Values.ShouldAllBe(entry => entry.EnumerationCount == 1);
    }

    [Fact]
    public void WideSubtreePreservesPerFragmentMatchesAndExclusions()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateTree(env);
        for (int index = 0; index < 32; index++)
        {
            string directory = Path.Combine(root, "src", $"group{index:D3}", "nested");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "settings.props"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "settings.targets"), string.Empty);
        }

        string[] specifications =
        [
            Normalize("Assets/**"),
            Normalize("**/*.props"),
            Normalize("src/**/settings.*"),
            Normalize("src/group031/nested/**"),
            Normalize("**/*.targets"),
            .. Enumerable.Range(0, 8).Select(index => Normalize($"**/*.unused{index}"))
        ];
        List<string> excludes = [Normalize("obj/**"), Normalize("src/group000/**")];
        FileMatcher reference = new(FileSystems.Default, new ConcurrentDictionary<string, IReadOnlyList<string>>());
        ConcurrentDictionary<string, CountingEntries> entries = new();
        FileMatcher matcher = CreateRecordingMatcher(entries);
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, excludes);

        foreach (string specification in specifications)
        {
            matcher.GetFiles(root, specification, excludes, expander).FileList.OrderBy(path => path, StringComparer.Ordinal).ShouldBe(
                reference.GetFiles(root, specification, excludes).FileList.OrderBy(path => path, StringComparer.Ordinal));
        }

        entries.Values.ShouldAllBe(entry => entry.EnumerationCount == 1);
        entries.Keys.ShouldNotContain("Files;" + Path.Combine(root, "src", "group000"));
    }

    [Fact]
    public void DifferentExcludesKeepSeparateResultsInTheSharedCache()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateTree(env);
        string[] specifications = [Normalize("**/*.props"), Normalize("**/*.targets")];
        FileMatcher matcher = new(FileSystems.Default, new ConcurrentDictionary<string, IReadOnlyList<string>>());
        List<string> firstExcludes = [Normalize("obj/**")];
        List<string> secondExcludes = [Normalize("Assets/**")];
        FileMatcher.GlobExpander first = new(matcher, root, specifications, firstExcludes);
        FileMatcher.GlobExpander second = new(matcher, root, specifications, secondExcludes);

        matcher.GetFiles(root, specifications[0], firstExcludes, first).FileList.ShouldContain(Normalize("Assets/styles/theme.props"));
        matcher.GetFiles(root, specifications[0], secondExcludes, second).FileList.ShouldNotContain(Normalize("Assets/styles/theme.props"));
        matcher.GetFiles(root, specifications[0], secondExcludes, second).FileList.ShouldContain(Normalize("obj/generated.props"));
    }

    [Fact]
    public void CompletedCacheHitDoesNotEnlargeALaterNarrowSearch()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateTree(env);
        ConcurrentDictionary<string, CountingEntries> entries = new();
        FileMatcher matcher = CreateRecordingMatcher(entries);
        string[] specifications = [Normalize("**/*"), Normalize("Assets/**/*.props")];
        matcher.GetFiles(root, specifications[0]);
        foreach (CountingEntries entry in entries.Values)
        {
            entry.Reset();
        }

        FileMatcher.GlobExpander expander = new(matcher, root, specifications, null);
        matcher.GetFiles(root, specifications[0], globExpander: expander);
        matcher.GetFiles(root, specifications[1], globExpander: expander).FileList.ShouldBe(
            [Normalize("Assets/styles/theme.props")]);
        entries.Where(entry => !entry.Key.Contains(Path.Combine(root, "Assets")))
            .ShouldAllBe(entry => entry.Value.EnumerationCount == 0);
    }

    [Fact]
    public void FailureInOneSubtreePreservesOtherFragmentsAndFailureReporting()
    {
        using TestEnvironment env = TestEnvironment.Create(_output);
        string root = CreateTree(env);
        for (int index = 0; index < 32; index++)
        {
            Directory.CreateDirectory(Path.Combine(root, "Assets", $"group{index:D3}"));
        }

        ConcurrentDictionary<string, CountingEntries> entries = new();
        FileMatcher matcher = CreateRecordingMatcher(entries, failDirectory: Path.Combine(root, "Assets", "group000"));
        string[] specifications = [Normalize("Assets/**/*.props"), Normalize("build/**/*.targets"), Normalize("**/*.props")];
        FileMatcher.GlobExpander expander = new(matcher, root, specifications, null);

        var failed = matcher.GetFiles(root, specifications[0], globExpander: expander);
        failed.FileList.ShouldBe([specifications[0]]);
        failed.GlobFailure.ShouldNotBeNullOrEmpty();
        var unaffected = matcher.GetFiles(root, specifications[1], globExpander: expander);
        unaffected.FileList.OrderBy(path => path, StringComparer.Ordinal).ShouldBe(
            [Normalize("build/Common.targets"), Normalize("build/generated/extra.targets")]);
        unaffected.GlobFailure.ShouldBeNull();
    }

    private static FileMatcher CreateRecordingMatcher(
        ConcurrentDictionary<string, CountingEntries> entries,
        string? failDirectory = null) =>
        new(
            FileSystems.Default,
            (type, path, _, _, _) =>
            {
                if (type == FileMatcher.FileSystemEntity.Files && path == failDirectory)
                {
                    throw new IOException("test enumeration failure");
                }

                string[] paths = type switch
                {
                    FileMatcher.FileSystemEntity.Files => Directory.GetFiles(path),
                    FileMatcher.FileSystemEntity.Directories => Directory.GetDirectories(path),
                    _ => Directory.GetFileSystemEntries(path)
                };
                CountingEntries result = new(paths);
                entries[type + ";" + path] = result;
                return result;
            },
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            allowDirectEnumeration: true);

    private static string CreateTree(TestEnvironment env)
    {
        string root = env.CreateFolder().Path;
        string[] paths =
        [
            "Directory.Build.props",
            "Assets/logo.svg",
            "Assets/styles/theme.props",
            "Assets/styles/ignored.tmp",
            "build/Common.targets",
            "build/generated/extra.targets",
            "obj/generated.props",
            "LICENSE"
        ];
        foreach (string path in paths)
        {
            string fullPath = Path.Combine(root, Normalize(path));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, string.Empty);
        }

        return root;
    }

    private static string Normalize(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    private sealed class CountingEntries(string[] entries) : IReadOnlyList<string>
    {
        private int _enumerationCount;

        internal int EnumerationCount => _enumerationCount;
        internal void Reset() => Interlocked.Exchange(ref _enumerationCount, 0);
        public int Count => entries.Length;
        public string this[int index] => entries[index];

        public IEnumerator<string> GetEnumerator()
        {
            Interlocked.Increment(ref _enumerationCount);
            return ((IEnumerable<string>)entries).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
