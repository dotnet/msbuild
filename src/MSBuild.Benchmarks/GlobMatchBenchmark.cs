// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Globbing;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class GlobMatchBenchmark
{
    private CompositeGlob _glob = null!;
    private string[] _paths = null!;

    [Params(4, 32)]
    public int ExcludeCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string root = Path.Combine(Path.GetTempPath(), nameof(GlobMatchBenchmark));
        IMSBuildGlob[] excludes = Enumerable.Range(0, ExcludeCount)
            .Select(i => MSBuildGlob.Parse(root, $"**/excluded{i}/**"))
            .ToArray();
        _glob = new CompositeGlob(
            new MSBuildGlobWithGaps(MSBuildGlob.Parse(root, "**/*.cs"), excludes),
            MSBuildGlob.Parse(root, "**/*.vb"));
        _paths = Enumerable.Range(0, 128)
            .Select(i => Path.Combine("src", $"directory{i}", "nested", "File.cs"))
            .ToArray();

        if (MatchFiles() != _paths.Length)
        {
            throw new InvalidOperationException("The glob must match every benchmark input.");
        }
    }

    [Benchmark]
    public int MatchFiles()
    {
        int matches = 0;
        foreach (string path in _paths)
        {
            if (_glob.IsMatch(path))
            {
                matches++;
            }
        }

        return matches;
    }
}
