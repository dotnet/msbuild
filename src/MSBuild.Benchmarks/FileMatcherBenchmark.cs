// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class FileMatcherBenchmark
{
    public enum GlobScenario
    {
        SimpleRecursive,
        DefaultExcludes,
        WildcardInMiddle,
        RepeatedAnchor,
        NoMatch,
    }

    private string _root = null!;
    private string _include = null!;
    private List<string>? _excludes;
    private FileMatcher _legacy = null!;
    private FileMatcher _optimized = null!;
    private FileMatcher _legacyCached = null!;
    private FileMatcher _optimizedCached = null!;

    [Params(32, 512)]
    public int SourceFileCount { get; set; }

    [ParamsAllValues]
    public GlobScenario Scenario { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(Path.GetTempPath(), nameof(FileMatcherBenchmark), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        CreateTree();
        ConfigureScenario();

        _legacy = new FileMatcher(FileSystems.Default, implementation: FileMatcherImplementation.Legacy);
        _optimized = new FileMatcher(FileSystems.Default, implementation: FileMatcherImplementation.Optimized);
        _legacyCached = new FileMatcher(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Legacy);
        _optimizedCached = new FileMatcher(
            FileSystems.Default,
            new ConcurrentDictionary<string, IReadOnlyList<string>>(),
            FileMatcherImplementation.Optimized);

        AssertEquivalent(
            _legacy.GetFiles(_root, _include, _excludes).FileList,
            _optimized.GetFiles(_root, _include, _excludes).FileList,
            "direct");

        // Prime the warm-cache benchmarks outside measurement.
        string[] legacyCached = _legacyCached.GetFiles(_root, _include, _excludes).FileList;
        string[] optimizedCached = _optimizedCached.GetFiles(_root, _include, _excludes).FileList;
        AssertEquivalent(legacyCached, optimizedCached, "cache-backed selection");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Cold")]
    public string[] Legacy() => _legacy.GetFiles(_root, _include, _excludes).FileList;

    [Benchmark]
    [BenchmarkCategory("Cold")]
    public string[] Optimized() => _optimized.GetFiles(_root, _include, _excludes).FileList;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("CacheBackedCold")]
    public string[] LegacyCacheBackedCold() => new FileMatcher(
        FileSystems.Default,
        new ConcurrentDictionary<string, IReadOnlyList<string>>(),
        FileMatcherImplementation.Legacy).GetFiles(_root, _include, _excludes).FileList;

    [Benchmark]
    [BenchmarkCategory("CacheBackedCold")]
    public string[] OptimizedSelectionCacheBackedCold() => new FileMatcher(
        FileSystems.Default,
        new ConcurrentDictionary<string, IReadOnlyList<string>>(),
        FileMatcherImplementation.Optimized).GetFiles(_root, _include, _excludes).FileList;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("WarmCache")]
    public string[] LegacyWarmCache() => _legacyCached.GetFiles(_root, _include, _excludes).FileList;

    [Benchmark]
    [BenchmarkCategory("WarmCache")]
    public string[] OptimizedWarmCache() => _optimizedCached.GetFiles(_root, _include, _excludes).FileList;

    private void AssertEquivalent(string[] legacy, string[] optimized, string driver)
    {
        Array.Sort(legacy, StringComparer.OrdinalIgnoreCase);
        Array.Sort(optimized, StringComparer.OrdinalIgnoreCase);
        if (!legacy.AsSpan().SequenceEqual(optimized))
        {
            throw new InvalidOperationException(
                $"Benchmark scenario {Scenario} produced different {driver} results: " +
                $"legacy={legacy.Length}, optimized={optimized.Length}.");
        }
    }

    private void CreateTree()
    {
        for (int index = 0; index < SourceFileCount; index++)
        {
            string directory = Path.Combine(
                _root,
                index % 11 == 0 ? "obj" : "src",
                $"group{index % 8}",
                index % 5 == 0 ? Path.Combine("a", "a", "b") : "nested");

            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"source{index}.cs"), string.Empty);

            if (index % 4 == 0)
            {
                File.WriteAllText(Path.Combine(directory, $"content{index}.txt"), string.Empty);
            }
        }

        string binDirectory = Path.Combine(_root, "bin", "Debug");
        Directory.CreateDirectory(binDirectory);
        File.WriteAllText(Path.Combine(binDirectory, "generated.cs"), string.Empty);
    }

    private void ConfigureScenario()
    {
        (_include, _excludes) = Scenario switch
        {
            GlobScenario.SimpleRecursive => ("**/*.cs", (List<string>?)null),
            GlobScenario.DefaultExcludes => (
                "**/*.cs",
                new List<string>
                {
                    "bin/Debug/**",
                    "obj/Debug/**",
                    "bin/**",
                    "obj/**",
                    "**/*.user",
                    "**/*.*proj",
                    "**/*.sln",
                    "**/*.slnx",
                    "**/*.vssscc",
                    "**/.DS_Store",
                }),
            GlobScenario.WildcardInMiddle => ("**/src/**/*.cs", (List<string>?)null),
            GlobScenario.RepeatedAnchor => ("**/a/b/*.cs", (List<string>?)null),
            GlobScenario.NoMatch => ("**/*.does-not-exist", (List<string>?)null),
            _ => throw new ArgumentOutOfRangeException(),
        };
    }
}