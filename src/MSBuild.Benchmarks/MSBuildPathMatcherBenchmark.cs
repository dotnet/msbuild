// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.Globbing;
using System.Text.RegularExpressions;

namespace MSBuild.Benchmarks;

[MemoryDiagnoser]
public class MSBuildPathMatcherBenchmark
{
    public enum PatternScenario
    {
        SimpleRecursive,
        WildcardInMiddle,
        RepeatedAnchor,
        MultipleGlobstars,
    }

    private Candidate[] _candidates = null!;
    private string _fileSpec = null!;
    private MSBuildPathMatcher _matcher = null!;
    private Regex _legacyRegex = null!;

    [ParamsAllValues]
    public PatternScenario Scenario { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        (string wildcardDirectory, string filePattern, _fileSpec) = Scenario switch
        {
            PatternScenario.SimpleRecursive => ("**", "*.cs", "**/*.cs"),
            PatternScenario.WildcardInMiddle => ("**/src/**", "*.cs", "**/src/**/*.cs"),
            PatternScenario.RepeatedAnchor => ("**/a/b", "*.cs", "**/a/b/*.cs"),
            PatternScenario.MultipleGlobstars => ("**/a/**/b/**", "*.cs", "**/a/**/b/**/*.cs"),
            _ => throw new ArgumentOutOfRangeException(),
        };

        wildcardDirectory = ToPlatformPath(wildcardDirectory);
        _fileSpec = ToPlatformPath(_fileSpec);
        _matcher = new MSBuildPathMatcher(
            wildcardDirectory,
            filePattern,
            useCultureSensitiveMatch: Scenario is PatternScenario.RepeatedAnchor or PatternScenario.MultipleGlobstars);
        _candidates = CreateCandidates();

        FileMatcher.Default.GetFileSpecInfoWithRegexObject(
            _fileSpec,
            out _legacyRegex,
            out _,
            out bool isLegalFileSpec);
        if (!isLegalFileSpec)
        {
            throw new InvalidOperationException($"Illegal benchmark file specification '{_fileSpec}'.");
        }

        foreach (Candidate candidate in _candidates)
        {
            bool legacy = _legacyRegex.IsMatch(candidate.FullPath);
            bool optimized = _matcher.MatchesFile(candidate.Directory, candidate.FileName);
            if (legacy != optimized)
            {
                throw new InvalidOperationException(
                    $"Matcher benchmark mismatch for '{_fileSpec}' and '{candidate.FullPath}'.");
            }
        }
    }

    [Benchmark(Baseline = true)]
    public int LegacyRegex()
    {
        int matches = 0;
        foreach (Candidate candidate in _candidates)
        {
            if (_legacyRegex.IsMatch(candidate.FullPath))
            {
                matches++;
            }
        }

        return matches;
    }

    [Benchmark]
    public int OptimizedStateSet()
    {
        int matches = 0;
        foreach (Candidate candidate in _candidates)
        {
            if (_matcher.MatchesFile(candidate.Directory, candidate.FileName))
            {
                matches++;
            }
        }

        return matches;
    }

    private static Candidate[] CreateCandidates()
    {
        Candidate[] candidates = new Candidate[256];
        for (int index = 0; index < candidates.Length; index++)
        {
            string directory = (index % 4) switch
            {
                0 => $"root/group{index % 7}/src/a/a/b/deep",
                1 => $"root/group{index % 7}/a/a/b",
                2 => $"root/group{index % 7}/a/middle/b/deep",
                _ => $"root/group{index % 7}/other",
            };

            directory = ToPlatformPath(directory);
            string fileName = index % 3 == 0 ? $"source{index}.txt" : $"source{index}.cs";
            candidates[index] = new Candidate(Path.Combine(directory, fileName), directory, fileName);
        }

        return candidates;
    }

    private static string ToPlatformPath(string path) => path
        .Replace('\\', Path.DirectorySeparatorChar)
        .Replace('/', Path.DirectorySeparatorChar);

    private readonly record struct Candidate(string FullPath, string Directory, string FileName);
}