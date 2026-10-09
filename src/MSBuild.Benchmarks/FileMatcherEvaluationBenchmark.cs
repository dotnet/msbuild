// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class FileMatcherEvaluationBenchmark
{
    private static readonly string[] s_defaultExcludes =
    [
        "bin/**",
        "obj/**",
        "**/bin/**",
        "**/obj/**",
        "**/*.user",
        "**/*.*proj",
        "**/*.sln",
        "**/*.slnx",
        "**/*.vssscc",
        "**/.DS_Store",
    ];

    private string _root = null!;
    private List<string>? _excludes;

    [Params(3, 32, 128)]
    public int DirectoryCount { get; set; }

    [Params(8, 64)]
    public int NonMatchingFilesPerDirectory { get; set; }

    [Params(0, 2, 4, 10)]
    public int ExcludeCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            nameof(FileMatcherEvaluationBenchmark),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        CreateTree();
        _excludes = ExcludeCount == 0
            ? null
            : s_defaultExcludes.Take(ExcludeCount).ToList();

        string[] legacy = Expand(FileMatcherImplementation.Legacy);
        string[] optimized = Expand(FileMatcherImplementation.Optimized);
        Array.Sort(legacy, StringComparer.OrdinalIgnoreCase);
        Array.Sort(optimized, StringComparer.OrdinalIgnoreCase);

        if (!legacy.AsSpan().SequenceEqual(optimized))
        {
            throw new InvalidOperationException(
                $"Evaluation benchmark results differ: legacy={legacy.Length}, optimized={optimized.Length}.");
        }
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
    public string[] Legacy() => Expand(FileMatcherImplementation.Legacy);

    [Benchmark]
    public string[] Optimized() => Expand(FileMatcherImplementation.Optimized);

    private string[] Expand(FileMatcherImplementation implementation) => new FileMatcher(
        FileSystems.Default,
        new ConcurrentDictionary<string, IReadOnlyList<string>>(),
        implementation).GetFiles(_root, "src/**/*.cs", _excludes).FileList;

    private void CreateTree()
    {
        for (int directoryIndex = 0; directoryIndex < DirectoryCount; directoryIndex++)
        {
            string directory = Path.Combine(_root, "src", $"group{directoryIndex:D3}", "nested");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "first.cs"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "second.cs"), string.Empty);

            for (int fileIndex = 0; fileIndex < NonMatchingFilesPerDirectory; fileIndex++)
            {
                File.WriteAllText(Path.Combine(directory, $"content{fileIndex:D3}.txt"), string.Empty);
            }

            string excludedDirectory = Path.Combine(directory, directoryIndex % 2 == 0 ? "obj" : "bin");
            Directory.CreateDirectory(excludedDirectory);
            File.WriteAllText(Path.Combine(excludedDirectory, "generated.cs"), string.Empty);
        }
    }
}