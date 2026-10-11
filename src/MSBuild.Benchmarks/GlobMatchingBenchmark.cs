// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.Shared.Globbing;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class GlobMatchingBenchmark
{
    private Regex _regex = null!;
    private GlobPattern _native = null!;
    private string _expression = null!;
    private readonly string[] _fileNames =
    [
        "src/generated/Program.cs", "src/generated/Program.CS", "src/generated/GeneratedAssemblyInfo.cs", "src/generated/readme.txt",
        "src/generated/project.csproj", "src/generated/source.cs", "src/generated/content.json", "src/generated/AssemblyInfo.cs",
    ];

    [Params(false, true)]
    public bool CompiledRegex { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _expression = GlobbingRegex.RegularExpressionFromFileSpec("", "**/generated/", "*.cs");
        _regex = CreateRegex();
        _native = CreateMatcher();

        foreach (string fileName in _fileNames)
        {
            if (_regex.IsMatch(fileName) != _native.IsMatch(fileName))
            {
                throw new InvalidOperationException($"Matchers disagree on {fileName}.");
            }
        }
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Construction")]
    public object ConstructRegexMatcher() => CreateRegex();

    [Benchmark]
    [BenchmarkCategory("Construction")]
    public object ConstructNativeMatcher() => CreateMatcher();

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Matching")]
    public int RegexMatching()
    {
        int matches = 0;
        foreach (string fileName in _fileNames)
        {
            if (_regex.IsMatch(fileName))
            {
                matches++;
            }
        }

        return matches;
    }

    [Benchmark]
    [BenchmarkCategory("Matching")]
    public int NativeMatching() => Match(_native);

    private int Match(GlobPattern matcher)
    {
        int matches = 0;
        foreach (string fileName in _fileNames)
        {
            if (matcher.IsMatch(fileName))
            {
                matches++;
            }
        }

        return matches;
    }

    private Regex CreateRegex() => new(
        _expression,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | (CompiledRegex ? RegexOptions.Compiled : RegexOptions.None));

    private static GlobPattern CreateMatcher() => new("", "**/generated/", "*.cs", useInvariantCulture: true);
}
