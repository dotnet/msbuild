// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Globbing;

public sealed class FileMatcherOptimized_Tests : IDisposable
{
    private readonly TestEnvironment _environment;

    public FileMatcherOptimized_Tests(ITestOutputHelper output)
    {
        _environment = TestEnvironment.Create(output);
    }

    public void Dispose() => _environment.Dispose();

    [Theory]
    [InlineData("**/*.cs", "**/obj/**")]
    [InlineData("**/*", "**/obj/*.txt")]
    [InlineData("**/*", "**/obj/**")]
    [InlineData("**/*.*", "**/*x.txt")]
    [InlineData("**/*.", null)]
    [InlineData("**/a/b/*.cs", null)]
    [InlineData("src/*/*.?s", null)]
    [InlineData("*.*", null)]
    [InlineData("missing/**/*.cs", null)]
    [InlineData("invalid.../**/*.cs", null)]
    public void OptimizedMatchesLegacy(string include, string? exclude)
    {
        TransientTestFolder root = _environment.CreateFolder();
        CreateTree(root.Path);

        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);
        List<string>? excludes = exclude is null ? null : [ToPlatformPath(exclude)];

        var legacyResult = legacy.GetFiles(root.Path, ToPlatformPath(include), excludes);
        var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath(include), excludes);

        optimizedResult.Action.ShouldBe(legacyResult.Action);
        optimizedResult.ExcludeFileSpec.ShouldBe(legacyResult.ExcludeFileSpec);
        optimizedResult.GlobFailure.ShouldBe(legacyResult.GlobFailure);
        optimizedResult.FileList.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ShouldBe(legacyResult.FileList.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(
        nameof(FileMatcherTest.GetFilesComplexGlobbingMatchingInfo.GetTestData),
        MemberType = typeof(FileMatcherTest.GetFilesComplexGlobbingMatchingInfo),
        DisableDiscoveryEnumeration = true)]
    public void OptimizedMatchesLegacyForComplexCorpus(FileMatcherTest.GetFilesComplexGlobbingMatchingInfo info)
    {
        TransientTestFolder root = _environment.CreateFolder();

        foreach (string relativePath in FileMatcherTest.GetFilesComplexGlobbingMatchingInfo.FilesToCreate)
        {
            string path = Path.Combine(root.Path, ToPlatformPath(relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
        }

        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);
        List<string>? excludes = info.Excludes?.Select(ToPlatformPath).ToList();

        var legacyResult = legacy.GetFiles(root.Path, ToPlatformPath(info.Include), excludes);
        var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath(info.Include), excludes);

        optimizedResult.Action.ShouldBe(legacyResult.Action);
        optimizedResult.ExcludeFileSpec.ShouldBe(legacyResult.ExcludeFileSpec);
        optimizedResult.GlobFailure.ShouldBe(legacyResult.GlobFailure);
        optimizedResult.FileList.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ShouldBe(legacyResult.FileList.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void OptimizedCacheMatchesLegacyAndReturnsCopies()
    {
        TransientTestFolder root = _environment.CreateFolder();
        CreateTree(root.Path);

        FileMatcher legacy = new(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);

        List<string> excludes = [ToPlatformPath("**/obj/**")];

        var legacyCold = legacy.GetFiles(root.Path, ToPlatformPath("**/*.cs"), excludes);
        var optimizedCold = optimized.GetFiles(root.Path, ToPlatformPath("**/*.cs"), excludes);
        AssertEquivalent(legacyCold, optimizedCold);

        optimizedCold.FileList[0] = "caller mutation";

        var legacyWarm = legacy.GetFiles(root.Path, ToPlatformPath("**/*.cs"), excludes);
        var optimizedWarm = optimized.GetFiles(root.Path, ToPlatformPath("**/*.cs"), excludes);
        AssertEquivalent(legacyWarm, optimizedWarm);
        optimizedWarm.FileList.ShouldNotContain("caller mutation");

        List<string> narrowerExcludes = [ToPlatformPath("**/obj/**"), "root.cs"];
        AssertEquivalent(
            legacy.GetFiles(root.Path, ToPlatformPath("**/*.cs"), narrowerExcludes),
            optimized.GetFiles(root.Path, ToPlatformPath("**/*.cs"), narrowerExcludes));
    }

    [Theory]
    [InlineData("**/*.*")]
    [InlineData("**/*.")]
    public void OptimizedCachePreservesCacheWrapperFilenameSemantics(string include)
    {
        TransientTestFolder root = _environment.CreateFolder();
        CreateTree(root.Path);
        FileMatcher legacy = new(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);

        AssertEquivalent(
            legacy.GetFiles(root.Path, ToPlatformPath(include)),
            optimized.GetFiles(root.Path, ToPlatformPath(include)));
    }

    [Theory]
    [InlineData("**/*.*", "README")]
    [InlineData("**/*.", null)]
    public void OptimizedCacheBackedTraversalPreservesFilenameSemantics(
        string include,
        string? expectedFile)
    {
        TransientTestFolder root = _environment.CreateFolder();
        CreateTree(root.Path);
        List<string> excludes = [ToPlatformPath("**/obj/**")];
        FileMatcher legacy = new(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);

        var legacyResult = legacy.GetFiles(root.Path, ToPlatformPath(include), excludes);
        var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath(include), excludes);

        AssertEquivalent(legacyResult, optimizedResult);
        if (expectedFile is not null)
        {
            optimizedResult.FileList.ShouldContain(expectedFile);
        }
    }

    [WindowsOnlyTheory]
    [InlineData("LICENSE.*")]
    [InlineData("LICE*.*")]
    public void DirectDriverPreservesWindowsDosWildcardSemantics(string include)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "LICENSE"), string.Empty);
        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        AssertEquivalent(
            legacy.GetFiles(root.Path, include),
            optimized.GetFiles(root.Path, include));
        optimized.GetFiles(root.Path, include).FileList.ShouldContain("LICENSE");
    }

    [WindowsOnlyTheory]
    [InlineData("LICENSE.*")]
    [InlineData("LICE*.*")]
    public void CallbackDriverPreservesWindowsDosWildcardSemantics(string include)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "LICENSE"), string.Empty);
        RecordingFileSystem fileSystem = new(FileSystems.Default);
        FileMatcher legacy = new(fileSystem, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(fileSystem, implementation: FileMatcherImplementation.Optimized);

        AssertEquivalent(
            legacy.GetFiles(root.Path, include),
            optimized.GetFiles(root.Path, include));
        optimized.GetFiles(root.Path, include).FileList.ShouldContain("LICENSE");
    }

    [Theory]
    [InlineData("*.*")]
    [InlineData("*.")]
    public void ProcessWideResultCacheDoesNotChangeMatcherSemantics(string include)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "LICENSE"), string.Empty);

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");

            FileMatcher.ClearCaches();
            FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
            var legacyResult = legacy.GetFiles(root.Path, include);

            FileMatcher.ClearCaches();
            FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);
            var optimizedResult = optimized.GetFiles(root.Path, include);

            AssertEquivalent(legacyResult, optimizedResult);
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessWideResultCachePartitionsCaseFolding(bool invariantFirst)
    {
        TransientTestFolder root = _environment.CreateFolder();
        foreach (string directoryName in new[] { "i", "ı" })
        {
            string directory = Path.Combine(root.Path, directoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "source.cs"), string.Empty);
        }

        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            FileMatcher.ClearCaches();

            FileMatcherCaseFolding[] modes = invariantFirst
                ? [FileMatcherCaseFolding.InvariantCulture, FileMatcherCaseFolding.LegacyCurrentCulture]
                : [FileMatcherCaseFolding.LegacyCurrentCulture, FileMatcherCaseFolding.InvariantCulture];

            foreach (FileMatcherCaseFolding mode in modes)
            {
                FileMatcher matcher = new(
                    FileSystems.Default,
                    implementation: FileMatcherImplementation.Optimized,
                    caseFolding: mode);
                var result = matcher.GetFiles(
                    root.Path,
                    ToPlatformPath("**/I/*.cs"),
                    [ToPlatformPath("**/obj/**")]);

                result.FileList.ShouldBe(
                [
                    mode == FileMatcherCaseFolding.InvariantCulture
                        ? ToPlatformPath("i/source.cs")
                        : ToPlatformPath("ı/source.cs"),
                ]);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessWideResultCachePartitionsLegacyCultures(bool turkishFirst)
    {
        TransientTestFolder root = _environment.CreateFolder();
        foreach (string directoryName in new[] { "i", "ı" })
        {
            string directory = Path.Combine(root.Path, directoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "source.cs"), string.Empty);
        }

        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            string[] cultureNames = turkishFirst ? ["tr-TR", "en-US"] : ["en-US", "tr-TR"];

            foreach (string cultureName in cultureNames)
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
                FileMatcher matcher = new(
                    FileSystems.Default,
                    implementation: FileMatcherImplementation.Optimized,
                    caseFolding: FileMatcherCaseFolding.LegacyCurrentCulture);
                var result = matcher.GetFiles(
                    root.Path,
                    ToPlatformPath("**/I/*.cs"),
                    [ToPlatformPath("**/obj/**")]);

                result.FileList.ShouldBe(
                [
                    cultureName == "tr-TR"
                        ? ToPlatformPath("ı/source.cs")
                        : ToPlatformPath("i/source.cs"),
                ]);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [LinuxOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessWideResultCachePartitionsOptimizedDrivers(bool callbackFirst)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "i.cs"), string.Empty);

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher direct = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized,
                caseFolding: FileMatcherCaseFolding.InvariantCulture);
            FileMatcher callback = new(
                FileSystems.Default,
                new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                FileMatcherImplementation.Optimized,
                FileMatcherCaseFolding.InvariantCulture);
            FileMatcher[] matchers = callbackFirst ? [callback, direct] : [direct, callback];

            foreach (FileMatcher matcher in matchers)
            {
                var result = matcher.GetFiles(
                    root.Path,
                    "**/I.cs",
                    ["**/obj/**"]);

                result.FileList.ShouldBe(ReferenceEquals(matcher, callback) ? ["i.cs"] : []);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [LinuxOnlyFact]
    public void ProcessWideResultCachePartitionsConcurrentOptimizedDrivers()
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "i.cs"), string.Empty);

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher direct = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized,
                caseFolding: FileMatcherCaseFolding.InvariantCulture);
            FileMatcher callback = new(
                FileSystems.Default,
                new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                FileMatcherImplementation.Optimized,
                FileMatcherCaseFolding.InvariantCulture);
            string[]? directResult = null;
            string[]? callbackResult = null;

            Parallel.Invoke(
                () => directResult = direct.GetFiles(root.Path, "**/I.cs", ["**/obj/**"]).FileList,
                () => callbackResult = callback.GetFiles(root.Path, "**/I.cs", ["**/obj/**"]).FileList);

            directResult.ShouldBeEmpty();
            callbackResult.ShouldBe(["i.cs"]);
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [LinuxOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessWideResultCachePartitionsOptimizedCallbackDrivers(bool cachedFirst)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "i.cs"), string.Empty);
        List<string> excludes = ["**/obj/**", "literal.cs"];

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher uncachedCallback = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized,
                caseFolding: FileMatcherCaseFolding.InvariantCulture);
            FileMatcher cachedCallback = new(
                FileSystems.Default,
                new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                FileMatcherImplementation.Optimized,
                FileMatcherCaseFolding.InvariantCulture);
            FileMatcher[] matchers = cachedFirst
                ? [cachedCallback, uncachedCallback]
                : [uncachedCallback, cachedCallback];

            foreach (FileMatcher matcher in matchers)
            {
                string[] result = matcher.GetFiles(root.Path, "**/I.cs", excludes).FileList;
                result.ShouldBe(ReferenceEquals(matcher, cachedCallback) ? ["i.cs"] : []);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [LinuxOnlyFact]
    public void ProcessWideResultCachePartitionsConcurrentOptimizedCallbackDrivers()
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "i.cs"), string.Empty);
        List<string> excludes = ["**/obj/**", "literal.cs"];

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher uncachedCallback = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized,
                caseFolding: FileMatcherCaseFolding.InvariantCulture);
            FileMatcher cachedCallback = new(
                FileSystems.Default,
                new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                FileMatcherImplementation.Optimized,
                FileMatcherCaseFolding.InvariantCulture);
            string[]? uncachedResult = null;
            string[]? cachedResult = null;

            Parallel.Invoke(
                () => uncachedResult = uncachedCallback.GetFiles(root.Path, "**/I.cs", excludes).FileList,
                () => cachedResult = cachedCallback.GetFiles(root.Path, "**/I.cs", excludes).FileList);

            uncachedResult.ShouldBeEmpty();
            cachedResult.ShouldBe(["i.cs"]);
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [LinuxOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessWideResultCachePartitionsOptimizedFallbackDrivers(bool cachedFirst)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "i.cs"), string.Empty);

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher uncachedFallback = new(
                new RecordingFileSystem(FileSystems.Default),
                implementation: FileMatcherImplementation.Optimized,
                caseFolding: FileMatcherCaseFolding.InvariantCulture);
            FileMatcher cachedFallback = new(
                FileSystems.Default,
                new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                FileMatcherImplementation.Optimized,
                FileMatcherCaseFolding.InvariantCulture);
            FileMatcher[] matchers = cachedFirst
                ? [cachedFallback, uncachedFallback]
                : [uncachedFallback, cachedFallback];

            foreach (FileMatcher matcher in matchers)
            {
                string[] result = matcher.GetFiles(root.Path, "**/I.cs").FileList;
                result.ShouldBe(ReferenceEquals(matcher, cachedFallback) ? ["i.cs"] : []);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [LinuxOnlyFact]
    public void ProcessWideResultCachePartitionsConcurrentOptimizedFallbackDrivers()
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "i.cs"), string.Empty);

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher uncachedFallback = new(
                new RecordingFileSystem(FileSystems.Default),
                implementation: FileMatcherImplementation.Optimized,
                caseFolding: FileMatcherCaseFolding.InvariantCulture);
            FileMatcher cachedFallback = new(
                FileSystems.Default,
                new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                FileMatcherImplementation.Optimized,
                FileMatcherCaseFolding.InvariantCulture);
            string[]? uncachedResult = null;
            string[]? cachedResult = null;

            Parallel.Invoke(
                () => uncachedResult = uncachedFallback.GetFiles(root.Path, "**/I.cs").FileList,
                () => cachedResult = cachedFallback.GetFiles(root.Path, "**/I.cs").FileList);

            uncachedResult.ShouldBeEmpty();
            cachedResult.ShouldBe(["i.cs"]);
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [LinuxOnlyTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ProcessWideResultCachePartitionsLegacyDrivers(bool callbackFirst, bool useAutoWithWaveDisabled)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "i.cs"), string.Empty);

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            if (useAutoWithWaveDisabled)
            {
                _environment.SetEnvironmentVariable(
                    "MSBUILDDISABLEFEATURESFROMVERSION",
                    ChangeWaves.Wave18_11.ToString());
                ChangeWaves.ResetStateForTests();
            }

            FileMatcher.ClearCaches();
            FileMatcherImplementation implementation = useAutoWithWaveDisabled
                ? FileMatcherImplementation.Auto
                : FileMatcherImplementation.Legacy;
            FileMatcher uncached = new(FileSystems.Default, implementation: implementation);
            FileMatcher callback = new(
                FileSystems.Default,
                new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                implementation);
            FileMatcher[] matchers = callbackFirst ? [callback, uncached] : [uncached, callback];

            foreach (FileMatcher matcher in matchers)
            {
                string[] result = matcher.GetFiles(root.Path, "**/I.cs").FileList;
                result.ShouldBe(ReferenceEquals(matcher, callback) ? ["i.cs"] : []);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
            ChangeWaves.ResetStateForTests();
        }
    }

    [LinuxOnlyFact]
    public void ProcessWideResultCachePartitionsConcurrentLegacyDrivers()
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "i.cs"), string.Empty);

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher uncached = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Legacy);
            FileMatcher callback = new(
                FileSystems.Default,
                new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                FileMatcherImplementation.Legacy);
            string[]? uncachedResult = null;
            string[]? callbackResult = null;

            Parallel.Invoke(
                () => uncachedResult = uncached.GetFiles(root.Path, "**/I.cs").FileList,
                () => callbackResult = callback.GetFiles(root.Path, "**/I.cs").FileList);

            uncachedResult.ShouldBeEmpty();
            callbackResult.ShouldBe(["i.cs"]);
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessWideResultCacheDistinguishesExcludeBoundaries(bool combinedExcludeFirst)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "a.cs"), string.Empty);
        File.WriteAllText(Path.Combine(root.Path, "b.cs"), string.Empty);
        List<string> separateExcludes = ["a.cs", "b.cs"];
        List<string> combinedExclude = ["a.csb.cs"];

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher matcher = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized);
            List<string>[] orderedExcludes = combinedExcludeFirst
                ? [combinedExclude, separateExcludes]
                : [separateExcludes, combinedExclude];

            foreach (List<string> excludes in orderedExcludes)
            {
                string[] result = matcher.GetFiles(root.Path, "*.cs", excludes).FileList;
                result.ShouldBe(
                    ReferenceEquals(excludes, combinedExclude) ? ["a.cs", "b.cs"] : [],
                    ignoreOrder: true);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [UnixOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessWideResultCacheDistinguishesIncludeAndExcludeBoundaries(bool invalidIncludeFirst)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "a.cs"), string.Empty);
        const string regularInclude = "*.cs";
        const string invalidInclude = "*.cs1:?;";

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher matcher = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized);
            (string Include, List<string>? Excludes)[] requests = invalidIncludeFirst
                ? [(invalidInclude, null), (regularInclude, ["?"])]
                : [(regularInclude, ["?"]), (invalidInclude, null)];

            foreach ((string include, List<string>? excludes) in requests)
            {
                string[] result = matcher.GetFiles(root.Path, include, excludes).FileList;
                result.ShouldBe(include == invalidInclude ? [invalidInclude] : ["a.cs"]);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [LinuxOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessWideResultCacheDistinguishesProjectDirectoryCasing(bool upperFirst)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string lowerDirectory = Path.Combine(root.Path, "case");
        string upperDirectory = Path.Combine(root.Path, "CASE");
        Directory.CreateDirectory(lowerDirectory);
        Directory.CreateDirectory(upperDirectory);
        File.WriteAllText(Path.Combine(lowerDirectory, "lower.cs"), string.Empty);
        File.WriteAllText(Path.Combine(upperDirectory, "upper.cs"), string.Empty);

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher matcher = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Legacy);
            string[] directories = upperFirst
                ? [upperDirectory, lowerDirectory]
                : [lowerDirectory, upperDirectory];

            foreach (string directory in directories)
            {
                string[] result = matcher.GetFiles(directory, "*.cs").FileList;
                result.ShouldBe([
                    ReferenceEquals(directory, upperDirectory) ? "upper.cs" : "lower.cs",
                ]);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessWideResultCacheDistinguishesProjectAndIncludeIdentity(bool nestedProjectFirst)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string sourceDirectory = Path.Combine(root.Path, "src");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "source.cs"), string.Empty);

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher matcher = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized);
            (string ProjectDirectory, string Include, string Expected)[] requests = nestedProjectFirst
                ? [(sourceDirectory, "**/*.cs", "source.cs"), (root.Path, "src/**/*.cs", ToPlatformPath("src/source.cs"))]
                : [(root.Path, "src/**/*.cs", ToPlatformPath("src/source.cs")), (sourceDirectory, "**/*.cs", "source.cs")];

            foreach ((string projectDirectory, string include, string expected) in requests)
            {
                matcher.GetFiles(projectDirectory, ToPlatformPath(include)).FileList.ShouldBe([expected]);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProcessWideResultCachePartitionsAbsoluteIncludeByProjectDirectory(bool secondProjectFirst)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string firstProject = Path.Combine(root.Path, "first");
        string secondProject = Path.Combine(root.Path, "second");
        string firstObjectDirectory = Path.Combine(firstProject, "obj");
        string secondObjectDirectory = Path.Combine(secondProject, "obj");
        Directory.CreateDirectory(firstObjectDirectory);
        Directory.CreateDirectory(secondObjectDirectory);
        string firstFile = Path.Combine(firstObjectDirectory, "first.cs");
        string secondFile = Path.Combine(secondObjectDirectory, "second.cs");
        File.WriteAllText(firstFile, string.Empty);
        File.WriteAllText(secondFile, string.Empty);
        string include = Path.Combine(root.Path, "**", "*.cs");
        List<string> excludes = [ToPlatformPath("obj/**")];

        try
        {
            _environment.SetEnvironmentVariable("MsBuildCacheFileEnumerations", "1");
            FileMatcher.ClearCaches();
            FileMatcher matcher = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized);
            (string ProjectDirectory, string Expected)[] requests = secondProjectFirst
                ? [(secondProject, firstFile), (firstProject, secondFile)]
                : [(firstProject, secondFile), (secondProject, firstFile)];

            foreach ((string projectDirectory, string expected) in requests)
            {
                matcher.GetFiles(projectDirectory, include, excludes).FileList.ShouldBe([expected]);
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
        }
    }

    [Fact]
    public void DirectDriverPreservesLexicalExcludeRoot()
    {
        TransientTestFolder root = _environment.CreateFolder();
        string frameworkDirectory = Path.Combine(root.Path, "src", "Framework");
        Directory.CreateDirectory(frameworkDirectory);
        File.WriteAllText(Path.Combine(frameworkDirectory, "source.cs"), string.Empty);

        string include = ToPlatformPath("src/Framework/**/*.cs");
        List<string> excludes = [ToPlatformPath("src/Framework/../Framework/**/*.cs")];
        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        AssertEquivalent(
            legacy.GetFiles(root.Path, include, excludes),
            optimized.GetFiles(root.Path, include, excludes));
        optimized.GetFiles(root.Path, include, excludes).FileList.ShouldContain(
            ToPlatformPath("src/Framework/source.cs"));
    }

    [Fact]
    public void RootedIncludeWithRelativeExcludeAndNoProjectDirectoryMatchesLegacy()
    {
        TransientTestFolder root = _environment.CreateFolder();
        CreateTree(root.Path);
        string include = Path.Combine(root.Path, ToPlatformPath("**/*.cs"));
        List<string> excludes = [ToPlatformPath("**/obj/**")];
        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        AssertEquivalent(
            legacy.GetFiles(projectDirectoryUnescaped: null, include, excludes),
            optimized.GetFiles(projectDirectoryUnescaped: null, include, excludes));
    }

    [Fact]
    public void RelativeIncludeWithoutProjectDirectoryUsesCallbackDisposition()
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "source.cs"), string.Empty);
        _environment.SetCurrentDirectory(root.Path);
        string include = ToPlatformPath("./*.cs");
        FileMatcher legacy = new(
            FileSystems.Default,
            implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(
            FileSystems.Default,
            implementation: FileMatcherImplementation.Optimized);

        AssertSelection(
            optimized.SelectDriver(projectDirectory: null, include, excludeSpecs: null),
            FileMatcherDriver.OptimizedCallback,
            FileMatcherFallbackReason.RelativeFileSpecWithoutProjectDirectory);
        AssertEquivalent(
            legacy.GetFiles(projectDirectoryUnescaped: null, include),
            optimized.GetFiles(projectDirectoryUnescaped: null, include));
        optimized.GetFiles(projectDirectoryUnescaped: null, include).FileList.ShouldBe(["source.cs"]);

        static void AssertSelection(
            FileMatcher.DriverSelection selection,
            FileMatcherDriver expectedDriver,
            FileMatcherFallbackReason expectedFallbackReason)
        {
            selection.Driver.ShouldBe(expectedDriver);
            selection.FallbackReason.ShouldBe(expectedFallbackReason);
        }
    }

    [Theory]
    [InlineData("obj/gen", "**/obj/**", false)]
    [InlineData("obj/gen", "**/obj/**", true)]
    [InlineData("bin/Debug", "**/bin/**", false)]
    [InlineData("bin/Debug", "**/bin/**", true)]
    [InlineData("node_modules/pkg", "**/node_modules/**", false)]
    [InlineData("node_modules/pkg", "**/node_modules/**", true)]
    [InlineData("src/obj", "**/o?j/**", false)]
    [InlineData("src/obj", "**/o?j/**", true)]
    public void AncestorExcludeDoesNotMatchIncludeFixedDirectory(
        string fixedDirectory,
        string exclude,
        bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string generatedDirectory = Path.Combine(root.Path, ToPlatformPath(fixedDirectory));
        string deeperDirectory = Path.Combine(generatedDirectory, "deeper");
        Directory.CreateDirectory(deeperDirectory);
        File.WriteAllText(Path.Combine(generatedDirectory, "g1.cs"), string.Empty);
        File.WriteAllText(Path.Combine(deeperDirectory, "g2.cs"), string.Empty);

        FileMatcher optimized = new(
            FileSystems.Default,
            useEntryCache ? new ConcurrentDictionary<string, IReadOnlyList<string>>() : null,
            FileMatcherImplementation.Optimized);

        var result = optimized.GetFiles(
            root.Path,
            ToPlatformPath($"{fixedDirectory}/**/*.cs"),
            [ToPlatformPath(exclude)]);

        result.FileList.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ShouldBe(
        [
            ToPlatformPath($"{fixedDirectory}/deeper/g2.cs"),
            ToPlatformPath($"{fixedDirectory}/g1.cs"),
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AncestorGlobstarExcludeMatchesIncludeFixedDirectory(bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string outputDirectory = Path.Combine(root.Path, "bin", "Debug", "net9.0");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(outputDirectory, "x.cs"), string.Empty);
        string include = ToPlatformPath("bin/Debug/**/*.cs");
        List<string> excludes = [ToPlatformPath("**/bin/Debug/**")];
        FileMatcher legacy = new(
            FileSystems.Default,
            useEntryCache ? new ConcurrentDictionary<string, IReadOnlyList<string>>() : null,
            FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(
            FileSystems.Default,
            useEntryCache ? new ConcurrentDictionary<string, IReadOnlyList<string>>() : null,
            FileMatcherImplementation.Optimized);

        var optimizedResult = optimized.GetFiles(root.Path, include, excludes);

        AssertEquivalent(legacy.GetFiles(root.Path, include, excludes), optimizedResult);
        optimizedResult.FileList.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("src/*/*.cs", false)]
    [InlineData("src/*/*.cs", true)]
    [InlineData("src/F*/**/*.cs", false)]
    [InlineData("src/F*/**/*.cs", true)]
    public void AncestorNonGlobstarExcludeUsesLegacyFallback(string exclude, bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string frameworkDirectory = Path.Combine(root.Path, "src", "Framework");
        Directory.CreateDirectory(frameworkDirectory);
        File.WriteAllText(Path.Combine(frameworkDirectory, "source.cs"), string.Empty);
        string include = ToPlatformPath("src/Framework/**/*.cs");
        List<string> excludes = [ToPlatformPath(exclude)];
        FileMatcher legacy = new(
            FileSystems.Default,
            useEntryCache ? new ConcurrentDictionary<string, IReadOnlyList<string>>() : null,
            FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(
            FileSystems.Default,
            useEntryCache ? new ConcurrentDictionary<string, IReadOnlyList<string>>() : null,
            FileMatcherImplementation.Optimized);

        FileMatcher.DriverSelection selection = optimized.SelectDriver(root.Path, include, excludes);
        selection.Driver.ShouldBe(FileMatcherDriver.Legacy);
        selection.FallbackReason.ShouldBe(FileMatcherFallbackReason.AncestorExcludeRequiresLexicalPrefix);
        AssertEquivalent(
            legacy.GetFiles(root.Path, include, excludes),
            optimized.GetFiles(root.Path, include, excludes));
        optimized.GetFiles(root.Path, include, excludes).FileList.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WildcardDirectoryDotSegmentRemainsLiteral(bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string directory = Path.Combine(root.Path, "a");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "source.cs"), string.Empty);

        FileMatcher optimized = new(
            FileSystems.Default,
            useEntryCache ? new ConcurrentDictionary<string, IReadOnlyList<string>>() : null,
            FileMatcherImplementation.Optimized);

        var result = optimized.GetFiles(
            root.Path,
            ToPlatformPath("*/./*.cs"),
            [ToPlatformPath("**/obj/**")]);

        result.FileList.ShouldBeEmpty();
    }

    [Fact]
    public void AutoUsesChangeWaveFallback()
    {
        TransientTestFolder root = _environment.CreateFolder();
        CreateTree(root.Path);

        try
        {
            _environment.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
            ChangeWaves.ResetStateForTests();

            DirectRecordingFileSystem enabledFileSystem = new(FileSystems.Default);
            FileMatcher enabled = new(enabledFileSystem);
            enabled.ResolvedImplementation
                .ShouldBe(FileMatcherImplementation.Optimized);
            enabled.ResolvedCaseFolding
                .ShouldBe(FileMatcherCaseFolding.InvariantCulture);
            var enabledResult = enabled.GetFiles(root.Path, ToPlatformPath("**/*.cs"));
            enabledFileSystem.EnumerationCalls.ShouldBeEmpty();

            _environment.SetEnvironmentVariable(
                "MSBUILDDISABLEFEATURESFROMVERSION",
                ChangeWaves.Wave18_11.ToString());
            ChangeWaves.ResetStateForTests();

            DirectRecordingFileSystem disabledFileSystem = new(FileSystems.Default);
            FileMatcher disabled = new(disabledFileSystem);
            disabled.ResolvedImplementation
                .ShouldBe(FileMatcherImplementation.Legacy);
            disabled.ResolvedCaseFolding
                .ShouldBe(FileMatcherCaseFolding.LegacyCurrentCulture);
            var disabledResult = disabled.GetFiles(root.Path, ToPlatformPath("**/*.cs"));
            disabledFileSystem.EnumerationCalls.ShouldNotBeEmpty();

            AssertEquivalent(disabledResult, enabledResult);
        }
        finally
        {
            ChangeWaves.ResetStateForTests();
        }
    }

    [Fact]
    public void DriverSelectionReportsEffectiveDriverAndFallbackReason()
    {
        string projectDirectory = _environment.DefaultTestDirectory.Path;
        List<string> excludes = [ToPlatformPath("**/obj/**")];
        FileMatcher legacy = new(
            FileSystems.Default,
            implementation: FileMatcherImplementation.Legacy);
        FileMatcher unavailable = new(
            new RecordingFileSystem(FileSystems.Default),
            implementation: FileMatcherImplementation.Optimized);
        FileMatcher callback = new(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);
        FileMatcher direct = new(
            FileSystems.Default,
            implementation: FileMatcherImplementation.Optimized);

        AssertSelection(
            legacy.SelectDriver(projectDirectory: null, "**/*.cs", excludes),
            FileMatcherDriver.Legacy,
            FileMatcherFallbackReason.LegacyImplementation);
        AssertSelection(
            unavailable.SelectDriver(projectDirectory: null, "**/*.cs", excludes),
            FileMatcherDriver.Legacy,
            FileMatcherFallbackReason.OptimizedImplementationUnavailable);
        AssertSelection(
            callback.SelectDriver(projectDirectory, "**/*.cs", excludeSpecs: null),
            FileMatcherDriver.Legacy,
            FileMatcherFallbackReason.CacheBackedWithoutApplicableExcludes);
        AssertSelection(
            callback.SelectDriver(projectDirectory, "src/**/*.cs", ["bin/**", "obj/**"]),
            FileMatcherDriver.Legacy,
            FileMatcherFallbackReason.CacheBackedWithoutApplicableExcludes);
        AssertSelection(
            direct.SelectDriver(projectDirectory, ToPlatformPath("**/"), excludes),
            FileMatcherDriver.Legacy,
            FileMatcherFallbackReason.UnsupportedFileSpec);
        AssertSelection(
            direct.SelectDriver(
                projectDirectory,
                Path.Combine(Path.GetPathRoot(projectDirectory)!, "**", "*.log"),
                excludes),
            FileMatcherDriver.Legacy,
            FileMatcherFallbackReason.UnsupportedFileSpec);
        string fileSystemRoot = Path.GetPathRoot(projectDirectory)!;
        AssertSelection(
            direct.SelectDriver(fileSystemRoot, "**/*.cs", excludeSpecs: null),
            FileMatcherDriver.Legacy,
            FileMatcherFallbackReason.UnsupportedFileSpec);
        AssertSelection(
            direct.SelectDriver(fileSystemRoot, "src/**/*.cs", ["**/obj/**"]),
            FileMatcherDriver.Legacy,
            FileMatcherFallbackReason.UnsupportedFileSpec);
        AssertSelection(
            callback.SelectDriver(projectDirectory, "**/*.cs", excludes),
            FileMatcherDriver.OptimizedCallback,
            FileMatcherFallbackReason.None);
        AssertSelection(
            direct.SelectDriver(projectDirectory, "**/*.cs", ["literal.cs"]),
            FileMatcherDriver.OptimizedCallback,
            FileMatcherFallbackReason.None);
        AssertSelection(
            direct.SelectDriver(projectDirectory, "**/*.cs", excludes),
            FileMatcherDriver.OptimizedDirect,
            FileMatcherFallbackReason.None);

        static void AssertSelection(
            FileMatcher.DriverSelection selection,
            FileMatcherDriver expectedDriver,
            FileMatcherFallbackReason expectedFallbackReason)
        {
            selection.Driver.ShouldBe(expectedDriver);
            selection.FallbackReason.ShouldBe(expectedFallbackReason);
        }
    }

    [Fact]
    public void LegacyCultureEscapeHatchKeepsOptimizedImplementationEnabled()
    {
        _environment.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
        _environment.SetEnvironmentVariable("MSBUILDUSELEGACYCULTURESENSITIVEFILEGLOBS", "1");
        ChangeWaves.ResetStateForTests();

        FileMatcher matcher = new(FileSystems.Default);

        matcher.ResolvedImplementation.ShouldBe(FileMatcherImplementation.Optimized);
        matcher.ResolvedCaseFolding.ShouldBe(FileMatcherCaseFolding.LegacyCurrentCulture);
    }

    [Fact]
    public void LegacyCultureEscapeHatchIsCapturedByTraits()
    {
        _environment.SetEnvironmentVariable("MSBUILDUSELEGACYCULTURESENSITIVEFILEGLOBS", null);
        Traits traits = new();
        traits.UseLegacyCultureSensitiveFileGlobs.ShouldBeFalse();

        _environment.SetEnvironmentVariable("MSBUILDUSELEGACYCULTURESENSITIVEFILEGLOBS", "1");
        traits.UseLegacyCultureSensitiveFileGlobs.ShouldBeFalse();
        new Traits().UseLegacyCultureSensitiveFileGlobs.ShouldBeTrue();
    }

    [Fact]
    public void ExplicitOptimizedUsesLegacyCultureWhenWaveIsDisabled()
    {
        TransientTestFolder root = _environment.CreateFolder();
        foreach (string directoryName in new[] { "i", "ı" })
        {
            string directory = Path.Combine(root.Path, directoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "source.cs"), string.Empty);
        }

        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            _environment.SetEnvironmentVariable(
                "MSBUILDDISABLEFEATURESFROMVERSION",
                ChangeWaves.Wave18_11.ToString());
            ChangeWaves.ResetStateForTests();
            DirectRecordingFileSystem fileSystem = new(FileSystems.Default);
            FileMatcher matcher = new(
                fileSystem,
                implementation: FileMatcherImplementation.Optimized);

            var result = matcher.GetFiles(
                root.Path,
                ToPlatformPath("**/I/*.cs"),
                [ToPlatformPath("**/obj/**")]);

            matcher.ResolvedImplementation.ShouldBe(FileMatcherImplementation.Optimized);
            matcher.ResolvedCaseFolding.ShouldBe(FileMatcherCaseFolding.LegacyCurrentCulture);
            fileSystem.EnumerationCalls.ShouldBeEmpty();
            result.FileList.ShouldBe([ToPlatformPath("ı/source.cs")]);
        }
        finally
        {
            ChangeWaves.ResetStateForTests();
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void OptimizedMatchesLegacyForPatternMatrix()
    {
        TransientTestFolder root = _environment.CreateFolder();
        CreateTree(root.Path);

        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        string[] includes =
        [
            "*",
            "*.*",
            "*.cs",
            "?.cs",
            "**",
            "**/*",
            "**/*.*",
            "**/*.",
            "**/*.cs",
            "**/*.?s",
            "src/**/*.cs",
            "src/*/*.cs",
            "*/**/*.cs",
            "**/obj/*",
            "**/a/b/*.cs",
            "**/a/**/b/*.cs",
            "a/**/b/**",
            "**/**/a/**/b/*.cs",
            "**/./*.cs",
            "**//**//*.cs",
            "**/",
            "obj/**/",
            "missing/**/*.cs",
            "invalid.../**/*.cs",
        ];

        string[]?[] excludes =
        [
            null,
            ["**/obj/**"],
            ["**/obj/*.txt"],
            ["**/*.txt"],
            ["src/**"],
            ["**/*x.txt"],
            ["root.cs"],
            ["invalid.../**"],
            ["**/obj/*.txt", "root.cs"],
        ];

        foreach (string include in includes)
        {
            foreach (string[]? excludeSet in excludes)
            {
                string platformInclude = ToPlatformPath(include);
                List<string>? platformExcludes = excludeSet?.Select(ToPlatformPath).ToList();
                string context = $"Include='{include}', Excludes='{string.Join(";", excludeSet ?? [])}'";

                AssertEquivalent(
                    legacy.GetFiles(root.Path, platformInclude, platformExcludes),
                    optimized.GetFiles(root.Path, platformInclude, platformExcludes),
                    context);
            }
        }
    }

    [Fact]
    public void OptimizedUsesInjectedFileSystemAndPrunesExcludedSubtree()
    {
        TransientTestFolder root = _environment.CreateFolder();
        CreateTree(root.Path);
        RecordingFileSystem fileSystem = new(FileSystems.Default);
        FileMatcher optimized = new(
            fileSystem,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);

        string[] files = optimized.GetFiles(
            root.Path,
            ToPlatformPath("**/*.cs"),
            [ToPlatformPath("**/obj/**")]).FileList;

        files.ShouldNotContain(path => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));
        fileSystem.EnumerationCalls.ShouldNotBeEmpty();
        fileSystem.EnumerationCalls.ShouldNotContain(
            call => string.Equals(Path.GetFileName(call.Path), "obj", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DirectCapableFileSystemBypassesEnumerationCallbacksWhenUncached()
    {
        TransientTestFolder root = _environment.CreateFolder();
        CreateTree(root.Path);
        DirectRecordingFileSystem fileSystem = new(FileSystems.Default);
        FileMatcher optimized = new(fileSystem, implementation: FileMatcherImplementation.Optimized);

        string[] files = optimized.GetFiles(root.Path, ToPlatformPath("**/*.cs")).FileList;

        files.ShouldNotBeEmpty();
        fileSystem.EnumerationCalls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DriveEnumeratingWildcardUsesLegacyTraversal(bool useAsExclude)
    {
        Helpers.ResetStateForDriveEnumeratingWildcardTests(_environment, "0");
        TransientTestFolder project = _environment.CreateFolder();
        string root = Path.GetPathRoot(project.Path)!;
        string driveEnumeratingWildcard = Path.Combine(root, "**", "*.log");
        int enumerationCalls = 0;

        IReadOnlyList<string> Enumerate(
            FileMatcher.FileSystemEntity entityType,
            string path,
            string pattern,
            string projectDirectory,
            bool stripProjectDirectory)
        {
            enumerationCalls++;
            return [];
        }

        DirectRecordingFileSystem fileSystem = new(FileSystems.Default);
        FileMatcher optimized = new(
            fileSystem,
            Enumerate,
            implementation: FileMatcherImplementation.Optimized,
            allowDirectEnumeration: true);

        var result = useAsExclude
            ? optimized.GetFiles(project.Path, ToPlatformPath("**/*.cs"), [driveEnumeratingWildcard])
            : optimized.GetFiles(projectDirectoryUnescaped: null, driveEnumeratingWildcard);

        result.Action.ShouldBe(FileMatcher.SearchAction.LogDriveEnumeratingWildcard);
        result.ExcludeFileSpec.ShouldBe(useAsExclude ? driveEnumeratingWildcard : string.Empty);
        enumerationCalls.ShouldBeGreaterThan(0);
        fileSystem.EnumerationCalls.ShouldBeEmpty();
    }

    [Fact]
    public void DirectDriverPreservesLexicalDotSegments()
    {
        TransientTestFolder root = _environment.CreateFolder();
        string projectDirectory = Path.Combine(root.Path, "project");
        string sharedDirectory = Path.Combine(root.Path, "shared");
        Directory.CreateDirectory(projectDirectory);
        Directory.CreateDirectory(sharedDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, "project.cs"), string.Empty);
        File.WriteAllText(Path.Combine(sharedDirectory, "shared.cs"), string.Empty);

        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        string dotInclude = ToPlatformPath("./*.cs");
        string parentInclude = ToPlatformPath("../shared/**/*.cs");

        AssertEquivalent(
            legacy.GetFiles(projectDirectory, dotInclude),
            optimized.GetFiles(projectDirectory, dotInclude));
        AssertEquivalent(
            legacy.GetFiles(projectDirectory, parentInclude),
            optimized.GetFiles(projectDirectory, parentInclude));

        optimized.GetFiles(projectDirectory, dotInclude).FileList
            .ShouldContain(ToPlatformPath("./project.cs"));
        optimized.GetFiles(projectDirectory, parentInclude).FileList
            .ShouldContain(ToPlatformPath("../shared/shared.cs"));
    }

    [Fact]
    public void DirectDriverPreservesRelativeOutputWithTrailingProjectSeparator()
    {
        TransientTestFolder root = _environment.CreateFolder();
        string sourceDirectory = Path.Combine(root.Path, "src");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(root.Path, "root.cs"), string.Empty);
        File.WriteAllText(Path.Combine(sourceDirectory, "source.cs"), string.Empty);
        string projectDirectory = root.Path + Path.DirectorySeparatorChar;
        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        var legacyResult = legacy.GetFiles(projectDirectory, ToPlatformPath("**/*.cs"));
        var optimizedResult = optimized.GetFiles(projectDirectory, ToPlatformPath("**/*.cs"));

        AssertEquivalent(legacyResult, optimizedResult);
        optimizedResult.FileList.ShouldBe(["root.cs", ToPlatformPath("src/source.cs")], ignoreOrder: true);
    }

    [Fact]
    public void IdenticalTrailingDotExcludeRemovesExtensionlessFiles()
    {
        TransientTestFolder root = _environment.CreateFolder();
        string subdirectory = Path.Combine(root.Path, "sub");
        Directory.CreateDirectory(subdirectory);
        File.WriteAllText(Path.Combine(root.Path, "README"), string.Empty);
        File.WriteAllText(Path.Combine(subdirectory, "LICENSE"), string.Empty);
        string include = ToPlatformPath("**/*.");
        List<string> excludes = [include];
        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        var legacyResult = legacy.GetFiles(root.Path, include, excludes);
        var optimizedResult = optimized.GetFiles(root.Path, include, excludes);

        AssertEquivalent(legacyResult, optimizedResult);
        optimizedResult.FileList.ShouldBeEmpty();
    }

    [LinuxOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegexModePreservesLinuxDriverCasing(bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string lowerDirectory = Path.Combine(root.Path, "src", "p", "obj");
        string upperDirectory = Path.Combine(root.Path, "src", "p", "Obj");
        Directory.CreateDirectory(lowerDirectory);
        Directory.CreateDirectory(upperDirectory);
        File.WriteAllText(Path.Combine(lowerDirectory, "lower.cs"), string.Empty);
        File.WriteAllText(Path.Combine(upperDirectory, "upper.cs"), string.Empty);
        List<string> excludes = [ToPlatformPath("**/unused/**")];
        ConcurrentDictionary<string, IReadOnlyList<string>>? legacyCache = useEntryCache ? new() : null;
        ConcurrentDictionary<string, IReadOnlyList<string>>? optimizedCache = useEntryCache ? new() : null;
        FileMatcher legacy = new(
            FileSystems.Default,
            legacyCache,
            FileMatcherImplementation.Legacy,
            FileMatcherCaseFolding.InvariantCulture);
        FileMatcher optimized = new(
            FileSystems.Default,
            optimizedCache,
            FileMatcherImplementation.Optimized,
            FileMatcherCaseFolding.InvariantCulture);

        var legacyResult = legacy.GetFiles(root.Path, ToPlatformPath("src/*/obj/**/*.cs"), excludes);
        var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath("src/*/obj/**/*.cs"), excludes);

        AssertEquivalent(legacyResult, optimizedResult);
        optimizedResult.FileList.ShouldBe(
            useEntryCache
                ? [ToPlatformPath("src/p/Obj/upper.cs"), ToPlatformPath("src/p/obj/lower.cs")]
                : [ToPlatformPath("src/p/obj/lower.cs")],
            ignoreOrder: true);
    }

    [WindowsOnlyFact]
    public void DriveRelativeSpecUsesLegacyDisposition()
    {
        TransientTestFolder root = _environment.CreateFolder();
        char drive = Path.GetPathRoot(root.Path)![0];
        string include = $"{drive}:*.does-not-exist";
        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        AssertEquivalent(
            legacy.GetFiles(projectDirectoryUnescaped: null, include),
            optimized.GetFiles(projectDirectoryUnescaped: null, include));
    }

    [UnixOnlyFact]
    public void DirectDriverActivatesExcludeRootIgnoringCase()
    {
        TransientTestFolder root = _environment.CreateFolder();
        string lowerDirectory = Path.Combine(root.Path, "src");
        string upperDirectory = Path.Combine(root.Path, "SRC");
        Directory.CreateDirectory(lowerDirectory);
        Directory.CreateDirectory(upperDirectory);
        File.WriteAllText(Path.Combine(lowerDirectory, "source.cs"), string.Empty);

        string include = ToPlatformPath("src/**/*.cs");
        List<string> excludes = [Path.Combine(upperDirectory, ToPlatformPath("**/*.cs"))];
        FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        AssertEquivalent(
            legacy.GetFiles(root.Path, include, excludes),
            optimized.GetFiles(root.Path, include, excludes));
    }

    [UnixOnlyFact]
    public void DirectDriverTreatsBackslashAsDirectoryNameCharacter()
    {
        TransientTestFolder root = _environment.CreateFolder();
        string directory = Path.Combine(root.Path, @"a\b");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "source.cs"), string.Empty);

        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath("*/*.cs"));

        optimizedResult.GlobFailure.ShouldBeNull();
        optimizedResult.FileList.ShouldContain(Path.Combine(@"a\b", "source.cs"));
    }

    [UnixOnlyFact]
    public void DirectDriverPreservesLeadingBackslashesInEntryNames()
    {
        TransientTestFolder root = _environment.CreateFolder();
        string directory = Path.Combine(root.Path, @"\dir");
        string subdirectory = Path.Combine(root.Path, "sub");
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(subdirectory);
        File.WriteAllText(Path.Combine(directory, "source.cs"), string.Empty);
        File.WriteAllText(Path.Combine(subdirectory, @"\source.cs"), string.Empty);
        Directory.GetFiles(subdirectory).Select(Path.GetFileName).ShouldContain(@"\source.cs");

        FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

        var allFilesResult = optimized.GetFiles(root.Path, ToPlatformPath("*/*"));
        var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath("*/*.cs"));

        allFilesResult.FileList.ShouldContain(Path.Combine("sub", @"\source.cs"));
        optimizedResult.GlobFailure.ShouldBeNull();
        optimizedResult.FileList.ShouldContain(Path.Combine(@"\dir", "source.cs"));
        optimizedResult.FileList.ShouldContain(Path.Combine("sub", @"\source.cs"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LogicalTrailingDotExcludeDoesNotRemoveExtensionlessFile(bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "README"), string.Empty);
        List<string> excludes = [ToPlatformPath("**/*.")];
        ConcurrentDictionary<string, IReadOnlyList<string>>? legacyCache = useEntryCache ? new() : null;
        ConcurrentDictionary<string, IReadOnlyList<string>>? optimizedCache = useEntryCache ? new() : null;
        FileMatcher legacy = new(FileSystems.Default, legacyCache, FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, optimizedCache, FileMatcherImplementation.Optimized);

        var legacyResult = legacy.GetFiles(root.Path, ToPlatformPath("**/*"), excludes);
        var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath("**/*"), excludes);

        AssertEquivalent(legacyResult, optimizedResult);
        optimizedResult.FileList.ShouldContain("README");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidLiteralExcludeUsesLegacyExactStringEquality(bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string directory = Path.Combine(root.Path, "folder...name");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "source.cs"), string.Empty);
        char alternateSeparator = Path.DirectorySeparatorChar == '/' ? '\\' : '/';
        List<string> exactExclude = [ToPlatformPath("folder...name/source.cs")];
        List<string> pathEquivalentExclude = [$"FOLDER...NAME{alternateSeparator}SOURCE.CS"];
        ConcurrentDictionary<string, IReadOnlyList<string>>? legacyCache = useEntryCache ? new() : null;
        ConcurrentDictionary<string, IReadOnlyList<string>>? optimizedCache = useEntryCache ? new() : null;
        FileMatcher legacy = new(FileSystems.Default, legacyCache, FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, optimizedCache, FileMatcherImplementation.Optimized);

        var legacyExactResult = legacy.GetFiles(root.Path, ToPlatformPath("**/*"), exactExclude);
        var optimizedExactResult = optimized.GetFiles(root.Path, ToPlatformPath("**/*"), exactExclude);
        var legacyPathEquivalentResult = legacy.GetFiles(root.Path, ToPlatformPath("**/*"), pathEquivalentExclude);
        var optimizedPathEquivalentResult = optimized.GetFiles(root.Path, ToPlatformPath("**/*"), pathEquivalentExclude);

        AssertEquivalent(legacyExactResult, optimizedExactResult);
        optimizedExactResult.FileList.ShouldBeEmpty();
        AssertEquivalent(legacyPathEquivalentResult, optimizedPathEquivalentResult);
        optimizedPathEquivalentResult.FileList.ShouldBe([ToPlatformPath("folder...name/source.cs")]);
    }

    [UnixOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidWildcardExcludeUsesExactIdentity(bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string directory = Path.Combine(root.Path, "folder...name");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "*.cs"), string.Empty);
        string include = ToPlatformPath("**/*");
        string exclude = ToPlatformPath("folder...name/*.cs");
        List<string> excludes = [exclude];
        FileMatcher legacy = new(
            FileSystems.Default,
            useEntryCache ? new ConcurrentDictionary<string, IReadOnlyList<string>>() : null,
            FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(
            FileSystems.Default,
            useEntryCache ? new ConcurrentDictionary<string, IReadOnlyList<string>>() : null,
            FileMatcherImplementation.Optimized);

        FileMatcher.DriverSelection selection = optimized.SelectDriver(root.Path, include, excludes);
        selection.Driver.ShouldBe(FileMatcherDriver.OptimizedCallback);
        selection.FallbackReason.ShouldBe(
            useEntryCache
                ? FileMatcherFallbackReason.None
                : FileMatcherFallbackReason.InvalidExcludeRequiresExactIdentity);
        AssertEquivalent(
            legacy.GetFiles(root.Path, include, excludes),
            optimized.GetFiles(root.Path, include, excludes));
        optimized.GetFiles(root.Path, include, excludes).FileList.ShouldBeEmpty();
    }

    [WindowsOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void LogicalDirectoryExcludeDoesNotUseDosWildcardSemantics(bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string directory = Path.Combine(root.Path, "LICENSE");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "source.cs"), string.Empty);
        List<string> excludes = [ToPlatformPath("LICENSE.*/**/*")];
        ConcurrentDictionary<string, IReadOnlyList<string>>? legacyCache = useEntryCache ? new() : null;
        ConcurrentDictionary<string, IReadOnlyList<string>>? optimizedCache = useEntryCache ? new() : null;
        FileMatcher legacy = new(FileSystems.Default, legacyCache, FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, optimizedCache, FileMatcherImplementation.Optimized);

        var legacyResult = legacy.GetFiles(root.Path, ToPlatformPath("**/*"), excludes);
        var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath("**/*"), excludes);

        AssertEquivalent(legacyResult, optimizedResult);
        optimizedResult.FileList.ShouldContain(ToPlatformPath("LICENSE/source.cs"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComplexGlobsPreserveCurrentCultureCasing(bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string dottedDirectory = Path.Combine(root.Path, "i");
        string dotlessDirectory = Path.Combine(root.Path, "\u0131");
        string anchorDirectory = Path.Combine(root.Path, "anchor");
        Directory.CreateDirectory(dottedDirectory);
        Directory.CreateDirectory(dotlessDirectory);
        Directory.CreateDirectory(anchorDirectory);
        File.WriteAllText(Path.Combine(dottedDirectory, "source.cs"), string.Empty);
        File.WriteAllText(Path.Combine(dotlessDirectory, "source.cs"), string.Empty);
        File.WriteAllText(Path.Combine(anchorDirectory, "i.cs"), string.Empty);
        File.WriteAllText(Path.Combine(anchorDirectory, "\u0131.cs"), string.Empty);
        List<string> excludes = [ToPlatformPath("**/obj/**")];
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            ConcurrentDictionary<string, IReadOnlyList<string>>? legacyCache = useEntryCache ? new() : null;
            ConcurrentDictionary<string, IReadOnlyList<string>>? optimizedCache = useEntryCache ? new() : null;
            FileMatcher legacy = new(FileSystems.Default, legacyCache, FileMatcherImplementation.Legacy);
            FileMatcher optimized = new(
                FileSystems.Default,
                optimizedCache,
                FileMatcherImplementation.Optimized,
                FileMatcherCaseFolding.LegacyCurrentCulture);

            var legacyDirectoryResult = legacy.GetFiles(root.Path, ToPlatformPath("**/I/*.cs"), excludes);
            var optimizedDirectoryResult = optimized.GetFiles(root.Path, ToPlatformPath("**/I/*.cs"), excludes);
            var legacyFileResult = legacy.GetFiles(root.Path, ToPlatformPath("**/anchor/I.cs"), excludes);
            var optimizedFileResult = optimized.GetFiles(root.Path, ToPlatformPath("**/anchor/I.cs"), excludes);

            AssertEquivalent(legacyDirectoryResult, optimizedDirectoryResult);
            AssertEquivalent(legacyFileResult, optimizedFileResult);
            optimizedDirectoryResult.FileList.ShouldBe([ToPlatformPath("\u0131/source.cs")]);
            optimizedFileResult.FileList.ShouldBe([ToPlatformPath("anchor/\u0131.cs")]);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("**/I/*.cs", "i/source.cs", "ı/source.cs")]
    [InlineData("**/I/**/*.cs", "i/source.cs", "i/source.cs")]
    [InlineData("**/I.cs", "i.cs", "i.cs")]
    public void CultureSensitivityIsLimitedToLegacyRegexShapes(
        string include,
        string expectedEnglish,
        string expectedTurkish)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string dottedDirectory = Path.Combine(root.Path, "i");
        string dotlessDirectory = Path.Combine(root.Path, "ı");
        Directory.CreateDirectory(dottedDirectory);
        Directory.CreateDirectory(dotlessDirectory);
        File.WriteAllText(Path.Combine(dottedDirectory, "source.cs"), string.Empty);
        File.WriteAllText(Path.Combine(dotlessDirectory, "source.cs"), string.Empty);
        File.WriteAllText(Path.Combine(root.Path, "i.cs"), string.Empty);
        File.WriteAllText(Path.Combine(root.Path, "ı.cs"), string.Empty);
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            AssertCulture("en-US", expectedEnglish);
            AssertCulture("tr-TR", expectedTurkish);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        void AssertCulture(string cultureName, string expected)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
            List<string> excludes = [ToPlatformPath("**/obj/**")];
            FileMatcher legacy = new(
                FileSystems.Default,
                new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                FileMatcherImplementation.Legacy);
            FileMatcher optimized = new(
                FileSystems.Default,
                new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                FileMatcherImplementation.Optimized,
                FileMatcherCaseFolding.LegacyCurrentCulture);

            var legacyResult = legacy.GetFiles(root.Path, ToPlatformPath(include), excludes);
            var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath(include), excludes);

            AssertEquivalent(legacyResult, optimizedResult, cultureName);
            optimizedResult.FileList.ShouldBe([ToPlatformPath(expected)]);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    [InlineData("tr-CY")]
    [InlineData("az-Latn-AZ")]
    [InlineData("az-Cyrl-AZ")]
    public void InvariantCaseFoldingMakesComplexGlobCultureInvariant(string cultureName)
    {
        TransientTestFolder root = _environment.CreateFolder();
        foreach (string directoryName in new[] { "i", "İ", "ı" })
        {
            string directory = Path.Combine(root.Path, directoryName);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "source.cs"), string.Empty);
        }

        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
            foreach (FileMatcherImplementation implementation in new[]
                     {
                         FileMatcherImplementation.Legacy,
                         FileMatcherImplementation.Optimized,
                     })
            {
                FileMatcher matcher = new(
                    FileSystems.Default,
                    new ConcurrentDictionary<string, IReadOnlyList<string>>(),
                    implementation,
                    FileMatcherCaseFolding.InvariantCulture);

                var result = matcher.GetFiles(
                    root.Path,
                    ToPlatformPath("**/I/*.cs"),
                    [ToPlatformPath("**/obj/**")]);

                result.FileList.ShouldBe([ToPlatformPath("i/source.cs")]);
            }
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void FileMatchUsesInvariantCaseFolding(string cultureName)
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
            FileMatcher.ClearCaches();
            FileMatcher matcher = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized,
                caseFolding: FileMatcherCaseFolding.InvariantCulture);
            string pattern = ToPlatformPath("**/I/*.cs");

            matcher.FileMatch(pattern, ToPlatformPath("i/source.cs")).isMatch.ShouldBeTrue();
            matcher.FileMatch(pattern, ToPlatformPath("İ/source.cs")).isMatch.ShouldBeFalse();
            matcher.FileMatch(pattern, ToPlatformPath("ı/source.cs")).isMatch.ShouldBeFalse();
        }
        finally
        {
            FileMatcher.ClearCaches();
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FileMatchRegexCachePartitionsCaseFoldingAndCulture(bool reverseOrder)
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
        (FileMatcherCaseFolding Mode, string CultureName)[] cases =
        [
            (FileMatcherCaseFolding.InvariantCulture, "tr-TR"),
            (FileMatcherCaseFolding.LegacyCurrentCulture, "tr-TR"),
            (FileMatcherCaseFolding.LegacyCurrentCulture, "en-US"),
        ];

        try
        {
            FileMatcher.ClearCaches();
            IEnumerable<(FileMatcherCaseFolding Mode, string CultureName)> orderedCases = reverseOrder
                ? cases.Reverse()
                : cases;

            foreach ((FileMatcherCaseFolding mode, string cultureName) in orderedCases)
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
                FileMatcher matcher = new(
                    FileSystems.Default,
                    implementation: FileMatcherImplementation.Optimized,
                    caseFolding: mode);
                string pattern = ToPlatformPath("**/I/*.cs");
                bool matchesDotted = matcher.FileMatch(pattern, ToPlatformPath("i/source.cs")).isMatch;
                bool matchesDottedUpper = matcher.FileMatch(pattern, ToPlatformPath("İ/source.cs")).isMatch;
                bool matchesDotless = matcher.FileMatch(pattern, ToPlatformPath("ı/source.cs")).isMatch;

                if (mode == FileMatcherCaseFolding.InvariantCulture)
                {
                    matchesDotted.ShouldBeTrue();
                    matchesDottedUpper.ShouldBeFalse();
                    matchesDotless.ShouldBeFalse();
                }
                else if (cultureName == "tr-TR")
                {
                    matchesDotted.ShouldBeFalse();
                    matchesDottedUpper.ShouldBeFalse();
                    matchesDotless.ShouldBeTrue();
                }
                else
                {
                    matchesDotted.ShouldBeTrue();
                    matchesDottedUpper.ShouldBeTrue();
                    matchesDotless.ShouldBeFalse();
                }
            }
        }
        finally
        {
            FileMatcher.ClearCaches();
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void LiteralIncludeWildcardExcludeUsesExplicitCaseFolding()
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            _environment.SetEnvironmentVariable("MSBUILDUSELEGACYCULTURESENSITIVEFILEGLOBS", "1");
            string include = ToPlatformPath("ı/source.cs");
            List<string> excludes = [ToPlatformPath("**/I/*.cs")];
            FileMatcher legacy = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized,
                caseFolding: FileMatcherCaseFolding.LegacyCurrentCulture);
            FileMatcher invariant = new(
                FileSystems.Default,
                implementation: FileMatcherImplementation.Optimized,
                caseFolding: FileMatcherCaseFolding.InvariantCulture);

            legacy.GetFiles(projectDirectoryUnescaped: null, include, excludes).FileList.ShouldBeEmpty();
            invariant.GetFiles(projectDirectoryUnescaped: null, include, excludes).FileList.ShouldBe([include]);
        }
        finally
        {
            FileMatcher.ClearCaches();
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [UnixOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrailingBackslashIncludeUsesLegacyDisposition(bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "source.cs"), string.Empty);
        ConcurrentDictionary<string, IReadOnlyList<string>>? legacyCache = useEntryCache ? new() : null;
        ConcurrentDictionary<string, IReadOnlyList<string>>? optimizedCache = useEntryCache ? new() : null;
        FileMatcher legacy = new(FileSystems.Default, legacyCache, FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, optimizedCache, FileMatcherImplementation.Optimized);
        List<string> excludes = ["unused"];

        var legacyResult = legacy.GetFiles(root.Path, "**\\", excludes);
        var optimizedResult = optimized.GetFiles(root.Path, "**\\", excludes);

        AssertEquivalent(legacyResult, optimizedResult);
    }

    [UnixOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TrailingBackslashExcludeUsesLegacyDisposition(bool useEntryCache)
    {
        TransientTestFolder root = _environment.CreateFolder();
        string objectDirectory = Path.Combine(root.Path, "obj");
        Directory.CreateDirectory(objectDirectory);
        File.WriteAllText(Path.Combine(objectDirectory, "generated.cs"), string.Empty);
        ConcurrentDictionary<string, IReadOnlyList<string>>? legacyCache = useEntryCache ? new() : null;
        ConcurrentDictionary<string, IReadOnlyList<string>>? optimizedCache = useEntryCache ? new() : null;
        FileMatcher legacy = new(FileSystems.Default, legacyCache, FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(FileSystems.Default, optimizedCache, FileMatcherImplementation.Optimized);
        List<string> excludes = ["obj\\**\\"];

        var legacyResult = legacy.GetFiles(root.Path, "**/*", excludes);
        var optimizedResult = optimized.GetFiles(root.Path, "**/*", excludes);

        AssertEquivalent(legacyResult, optimizedResult);
        optimizedResult.FileList.ShouldContain(ToPlatformPath("obj/generated.cs"));
    }

    [UnixOnlyFact]
    public void CacheBackedDriverTreatsBackslashAsFileNameCharacter()
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, @"a\b.cs"), string.Empty);
        File.WriteAllText(Path.Combine(root.Path, "b.cs"), string.Empty);
        List<string> excludes = [ToPlatformPath("**/obj/**")];

        FileMatcher legacy = new(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);

        var legacyResult = legacy.GetFiles(root.Path, ToPlatformPath("**/b.cs"), excludes);
        var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath("**/b.cs"), excludes);

        AssertEquivalent(legacyResult, optimizedResult);
        optimizedResult.FileList.ShouldBe(["b.cs"]);
    }

    [UnixOnlyFact]
    public void CacheBackedExcludeDoesNotTreatBackslashSiblingAsDescendant()
    {
        TransientTestFolder root = _environment.CreateFolder();
        string excludedDirectory = Path.Combine(root.Path, "a");
        string siblingDirectory = Path.Combine(root.Path, @"a\b");
        Directory.CreateDirectory(excludedDirectory);
        List<string> excludes = [ToPlatformPath("a/**/*")];

        IReadOnlyList<string> Enumerate(
            FileMatcher.FileSystemEntity entityType,
            string path,
            string pattern,
            string projectDirectory,
            bool stripProjectDirectory)
        {
            if (entityType == FileMatcher.FileSystemEntity.Directories
                && FileUtilities.PathsEqual(path, root.Path))
            {
                return [excludedDirectory, siblingDirectory];
            }

            if (entityType == FileMatcher.FileSystemEntity.Files
                && FileUtilities.PathsEqual(path, excludedDirectory))
            {
                return [Path.Combine(excludedDirectory, "excluded.cs")];
            }

            if (entityType == FileMatcher.FileSystemEntity.Files
                && string.Equals(path, siblingDirectory, StringComparison.Ordinal))
            {
                return [Path.Combine(siblingDirectory, "source.cs")];
            }

            return [];
        }

        FileMatcher legacy = new(
            FileSystems.Default,
            Enumerate,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Legacy,
            allowDirectEnumeration: true);
        FileMatcher optimized = new(
            FileSystems.Default,
            Enumerate,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized,
            allowDirectEnumeration: true);

        var legacyResult = legacy.GetFiles(root.Path, ToPlatformPath("**/*"), excludes);
        var optimizedResult = optimized.GetFiles(root.Path, ToPlatformPath("**/*"), excludes);

        AssertEquivalent(legacyResult, optimizedResult);
        optimizedResult.FileList.ShouldBe([Path.Combine(@"a\b", "source.cs")]);
    }

    [Fact]
    public void CustomEnumerationDelegateRemainsAuthoritative()
    {
        TransientTestFolder root = _environment.CreateFolder();
        File.WriteAllText(Path.Combine(root.Path, "physical.cs"), string.Empty);
        int enumerationCalls = 0;

        IReadOnlyList<string> Enumerate(
            FileMatcher.FileSystemEntity entityType,
            string path,
            string pattern,
            string projectDirectory,
            bool stripProjectDirectory)
        {
            enumerationCalls++;
            return [];
        }

        FileMatcher optimized = new(
            FileSystems.Default,
            Enumerate,
            implementation: FileMatcherImplementation.Optimized);

        optimized.GetFiles(root.Path, ToPlatformPath("**/*.cs")).FileList.ShouldBeEmpty();
        enumerationCalls.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void ExternalStyleFileSystemWithoutEntryCacheUsesLegacyImplementation()
    {
        RecordingFileSystem fileSystem = new(FileSystems.Default);
        FileMatcher optimized = new(fileSystem, implementation: FileMatcherImplementation.Optimized);

        optimized.ResolvedImplementation.ShouldBe(FileMatcherImplementation.Optimized);
        optimized.GetFiles(_environment.DefaultTestDirectory.Path, "**/*.does-not-exist");
        fileSystem.EnumerationCalls.ShouldContain(
            call => call.Operation == nameof(IFileSystem.EnumerateFiles)
                && call.Pattern == "*.does-not-exist");
    }

    [Fact]
    public void EntryCacheUsesOptimizedCallbackDriverWhenExcludesArePresent()
    {
        TransientTestFolder root = _environment.CreateFolder();
        CreateTree(root.Path);
        DirectRecordingFileSystem fileSystem = new(FileSystems.Default);
        FileMatcher optimized = new(
            fileSystem,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);

        string[] files = optimized.GetFiles(
            root.Path,
            ToPlatformPath("**/*.cs"),
            [ToPlatformPath("**/obj/**")]).FileList;

        files.ShouldNotBeEmpty();
        fileSystem.EnumerationCalls.ShouldNotBeEmpty();
    }

    [Fact]
    public void OptimizedIoFailureMatchesLegacyDisposition()
    {
        TransientTestFolder root = _environment.CreateFolder();
        ThrowingEnumerationFileSystem legacyFileSystem = new(FileSystems.Default);
        ThrowingEnumerationFileSystem optimizedFileSystem = new(FileSystems.Default);
        FileMatcher legacy = new(
            legacyFileSystem,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Legacy);
        FileMatcher optimized = new(
            optimizedFileSystem,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);
        string include = ToPlatformPath("**/*.cs");
        List<string> excludes = [ToPlatformPath("**/obj/**")];

        var legacyResult = legacy.GetFiles(root.Path, include, excludes);
        var optimizedResult = optimized.GetFiles(root.Path, include, excludes);

        optimizedResult.FileList.ShouldBe(legacyResult.FileList);
        optimizedResult.Action.ShouldBe(legacyResult.Action);
        optimizedResult.ExcludeFileSpec.ShouldBe(legacyResult.ExcludeFileSpec);
        optimizedResult.GlobFailure.ShouldNotBeNull();
        optimizedResult.GlobFailure.ShouldContain(include);
    }

    [Fact]
    public void OptimizedNonIoFailurePropagates()
    {
        TransientTestFolder root = _environment.CreateFolder();
        NonIoThrowingEnumerationFileSystem fileSystem = new(FileSystems.Default);
        FileMatcher optimized = new(
            fileSystem,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);

        Should.Throw<InvalidOperationException>(() => optimized.GetFiles(
            root.Path,
            ToPlatformPath("**/*.cs"),
            [ToPlatformPath("**/obj/**")]));
    }

    [Fact]
    public void OptimizedAggregateIoFailureMatchesLegacyDisposition()
    {
        TransientTestFolder root = _environment.CreateFolder();
        AggregateIoThrowingEnumerationFileSystem fileSystem = new(FileSystems.Default);
        FileMatcher optimized = new(
            fileSystem,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);
        string include = ToPlatformPath("**/*.cs");

        var result = optimized.GetFiles(
            root.Path,
            include,
            [ToPlatformPath("**/obj/**")]);

        result.FileList.ShouldBe([include]);
        result.GlobFailure.ShouldNotBeNull();
        result.GlobFailure.ShouldContain(include);
    }

    [Fact]
    public void ParallelCallbackChildIoFailureMatchesLegacyDisposition()
    {
        TransientTestFolder root = _environment.CreateFolder();
        string firstChild = Path.Combine(root.Path, "first");
        string secondChild = Path.Combine(root.Path, "second");
        string include = ToPlatformPath("**/*.cs");

        IReadOnlyList<string> Enumerate(
            FileMatcher.FileSystemEntity entityType,
            string path,
            string pattern,
            string projectDirectory,
            bool stripProjectDirectory)
        {
            if (entityType == FileMatcher.FileSystemEntity.Directories
                && FileUtilities.PathsEqual(path, root.Path))
            {
                return [firstChild, secondChild];
            }

            if (entityType == FileMatcher.FileSystemEntity.Files
                && (FileUtilities.PathsEqual(path, firstChild)
                    || FileUtilities.PathsEqual(path, secondChild)))
            {
                throw new IOException("Injected child enumeration failure.");
            }

            return [];
        }

        FileMatcher optimized = new(
            FileSystems.Default,
            Enumerate,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized,
            allowDirectEnumeration: true);

        var result = optimized.GetFiles(
            root.Path,
            include,
            [ToPlatformPath("**/obj/**")]);

        result.FileList.ShouldBe([include]);
        result.Action.ShouldBe(FileMatcher.SearchAction.RunSearch);
        result.GlobFailure.ShouldNotBeNull();
        result.GlobFailure.ShouldContain("Injected child enumeration failure.");
    }

    [Fact]
    public void OptimizedMixedAggregateFailurePropagates()
    {
        TransientTestFolder root = _environment.CreateFolder();
        MixedAggregateThrowingEnumerationFileSystem fileSystem = new(FileSystems.Default);
        FileMatcher optimized = new(
            fileSystem,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);

        Should.Throw<AggregateException>(() => optimized.GetFiles(
            root.Path,
            ToPlatformPath("**/*.cs"),
            [ToPlatformPath("**/obj/**")]));
    }

#if FEATURE_SYMLINK_TARGET
    [RequiresSymbolicLinksFact]
    public void DirectDriverRejectsRecursiveSymlinkEnumerationRoot()
    {
        TransientTestFolder project = _environment.CreateFolder();
        string subdirectory = Path.Combine(project.Path, "sub");
        string link = Path.Combine(subdirectory, "loop");
        Directory.CreateDirectory(subdirectory);
        File.WriteAllText(Path.Combine(project.Path, "root.cs"), string.Empty);

        try
        {
            Directory.CreateSymbolicLink(link, project.Path);

            string include = ToPlatformPath("sub/loop/**/*.cs");
            FileMatcher legacy = new(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
            FileMatcher optimized = new(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);

            AssertEquivalent(
                legacy.GetFiles(project.Path, include),
                optimized.GetFiles(project.Path, include));
            optimized.GetFiles(project.Path, include).FileList.ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
        }
    }
#endif

    private static void CreateTree(string root)
    {
        string[] files =
        [
            "root.cs",
            "README",
            "suffix.txt",
            "subdir/bing",
            "src/one/source.cs",
            "src/two/source.fs",
            "a/a/b/repeated.cs",
            "obj/excluded.cs",
            "obj/ignored.txt",
            "deep/obj/excluded.cs",
            "deep/kept.cs",
        ];

        foreach (string relativePath in files)
        {
            string path = Path.Combine(root, ToPlatformPath(relativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, string.Empty);
        }
    }

    private static void AssertEquivalent(
        (string[] FileList, FileMatcher.SearchAction Action, string ExcludeFileSpec, string? GlobFailure) expected,
        (string[] FileList, FileMatcher.SearchAction Action, string ExcludeFileSpec, string? GlobFailure) actual,
        string? context = null)
    {
        actual.Action.ShouldBe(expected.Action, context);
        actual.ExcludeFileSpec.ShouldBe(expected.ExcludeFileSpec, context);
        actual.GlobFailure.ShouldBe(expected.GlobFailure, context);
        actual.FileList.OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ShouldBe(expected.FileList.OrderBy(path => path, StringComparer.OrdinalIgnoreCase), context);
    }

    private static string ToPlatformPath(string path) => path
        .Replace('\\', Path.DirectorySeparatorChar)
        .Replace('/', Path.DirectorySeparatorChar);

    private class RecordingFileSystem : IFileSystem
    {
        private readonly IFileSystem _inner;

        internal RecordingFileSystem(IFileSystem inner)
        {
            _inner = inner;
        }

        internal List<(string Operation, string Path, string Pattern)> EnumerationCalls { get; } = [];

        public TextReader ReadFile(string path) => _inner.ReadFile(path);
        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share) =>
            _inner.GetFileStream(path, mode, access, share);
        public string ReadFileAllText(string path) => _inner.ReadFileAllText(path);
        public byte[] ReadFileAllBytes(string path) => _inner.ReadFileAllBytes(path);

        public virtual IEnumerable<string> EnumerateFiles(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            EnumerationCalls.Add((nameof(EnumerateFiles), path, searchPattern));
            return _inner.EnumerateFiles(path, searchPattern, searchOption);
        }

        public virtual IEnumerable<string> EnumerateDirectories(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            EnumerationCalls.Add((nameof(EnumerateDirectories), path, searchPattern));
            return _inner.EnumerateDirectories(path, searchPattern, searchOption);
        }

        public virtual IEnumerable<string> EnumerateFileSystemEntries(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            EnumerationCalls.Add((nameof(EnumerateFileSystemEntries), path, searchPattern));
            return _inner.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

        public FileAttributes GetAttributes(string path) => _inner.GetAttributes(path);
        public DateTime GetLastWriteTimeUtc(string path) => _inner.GetLastWriteTimeUtc(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public bool FileExists(string path) => _inner.FileExists(path);
        public bool FileOrDirectoryExists(string path) => _inner.FileOrDirectoryExists(path);
    }

    private sealed class ThrowingEnumerationFileSystem : RecordingFileSystem
    {
        internal ThrowingEnumerationFileSystem(IFileSystem inner)
            : base(inner)
        {
        }

        public override IEnumerable<string> EnumerateFiles(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
            throw new IOException("Injected enumeration failure.");

        public override IEnumerable<string> EnumerateDirectories(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
            throw new IOException("Injected enumeration failure.");
    }

    private sealed class NonIoThrowingEnumerationFileSystem : RecordingFileSystem
    {
        internal NonIoThrowingEnumerationFileSystem(IFileSystem inner)
            : base(inner)
        {
        }

        public override IEnumerable<string> EnumerateFiles(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
            throw new InvalidOperationException("Injected non-I/O failure.");

        public override IEnumerable<string> EnumerateDirectories(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
            throw new InvalidOperationException("Injected non-I/O failure.");
    }

    private sealed class AggregateIoThrowingEnumerationFileSystem : RecordingFileSystem
    {
        internal AggregateIoThrowingEnumerationFileSystem(IFileSystem inner)
            : base(inner)
        {
        }

        public override IEnumerable<string> EnumerateFiles(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
            throw new AggregateException(
                new IOException("Injected I/O failure."),
                new AggregateException(new UnauthorizedAccessException("Injected access failure.")));

        public override IEnumerable<string> EnumerateDirectories(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
            throw new AggregateException(new IOException("Injected I/O failure."));
    }

    private sealed class MixedAggregateThrowingEnumerationFileSystem : RecordingFileSystem
    {
        internal MixedAggregateThrowingEnumerationFileSystem(IFileSystem inner)
            : base(inner)
        {
        }

        public override IEnumerable<string> EnumerateFiles(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
            throw new AggregateException(
                new IOException("Injected I/O failure."),
                new InvalidOperationException("Injected non-I/O failure."));

        public override IEnumerable<string> EnumerateDirectories(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly) =>
            throw new AggregateException(
                new IOException("Injected I/O failure."),
                new InvalidOperationException("Injected non-I/O failure."));
    }

    private sealed class DirectRecordingFileSystem : RecordingFileSystem, IDirectFileSystemEnumeration
    {
        internal DirectRecordingFileSystem(IFileSystem inner)
            : base(inner)
        {
        }

        public bool SupportsDirectEnumeration => true;
    }
}