// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests.Shared;
using Microsoft.Build.Shared.Globbing;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Globbing;

public class MSBuildPathMatcher_Tests
{
    [Theory]
    [InlineData("**/a/b", "a/a/b", "example.cs")]
    [InlineData("**/a/a", "a/a/a", "example.cs")]
    [InlineData("**/a/**/a", "a/a", "example.cs")]
    [InlineData("**/a/**/a", "root/a/middle/deep/a", "example.cs")]
    [InlineData("**/**/a", "root/deep/a", "example.cs")]
    public void GlobstarBacktracksAcrossRepeatedAnchors(string wildcardDirectory, string directory, string fileName)
    {
        MSBuildPathMatcher matcher = new(ToPlatformPath(wildcardDirectory), "*.cs");

        matcher.MatchesFile(ToPlatformPath(directory), fileName).ShouldBeTrue();
    }

    [Fact]
    public void GlobstarSuffixCanSpanParentAndChildDirectoryInputs()
    {
        MSBuildPathMatcher matcher = new(ToPlatformPath("**/a/b"), "*.cs");

        matcher.MatchesDirectory(ToPlatformPath("a/a"), "b")
            .ShouldBe(DirectoryMatchType.MayContainMatchingFiles);
    }

    [Fact]
    public void MiddleGlobstarDistinguishesPartialAndDeadDirectoryStates()
    {
        MSBuildPathMatcher matcher = new(ToPlatformPath("src/**/generated"), "*.cs");

        matcher.MatchesDirectory("src", "other")
            .ShouldBe(DirectoryMatchType.MayContainMatchingFiles);
        matcher.MatchesDirectory(ReadOnlySpan<char>.Empty, "other")
            .ShouldBe(DirectoryMatchType.NoDescendantFilesMatch);
        matcher.MatchesDirectory(ToPlatformPath("src/other"), "generated")
            .ShouldBe(DirectoryMatchType.MayContainMatchingFiles);
    }

    [Fact]
    public void MultipleGlobstarsDistinguishIncompleteAndDeadDirectoryStates()
    {
        MSBuildPathMatcher matcher = new(ToPlatformPath("src/**/a/**/b"), "*.cs");

        matcher.MatchesDirectory(ToPlatformPath("src/other"), "a")
            .ShouldBe(DirectoryMatchType.MayContainMatchingFiles);
        matcher.MatchesDirectory(ReadOnlySpan<char>.Empty, "other")
            .ShouldBe(DirectoryMatchType.NoDescendantFilesMatch);
        matcher.MatchesDirectory(ToPlatformPath("src/other/a/deep"), "b")
            .ShouldBe(DirectoryMatchType.MayContainMatchingFiles);
    }

    [Theory]
    [InlineData("**/a/b", "a/a/c", "example.cs")]
    [InlineData("**/a/a", "a/b/a", "example.cs")]
    [InlineData("**/a/**/a", "a/b/c", "example.cs")]
    [InlineData("src/*/generated", "src/one/other", "example.cs")]
    public void NonmatchingDirectoriesAreRejected(string wildcardDirectory, string directory, string fileName)
    {
        MSBuildPathMatcher matcher = new(ToPlatformPath(wildcardDirectory), "*.cs");

        matcher.MatchesFile(ToPlatformPath(directory), fileName).ShouldBeFalse();
    }

    [Fact]
    public void FileExcludeDoesNotPruneDirectory()
    {
        MSBuildPathMatcher matcher = new(ToPlatformPath("**/obj"), "*.txt");

        matcher.MatchesDirectory(ReadOnlySpan<char>.Empty, ToPlatformPath("src/obj"))
            .ShouldBe(DirectoryMatchType.MayContainMatchingFiles);
        matcher.MatchesFile(ToPlatformPath("src/obj"), "excluded.txt").ShouldBeTrue();
        matcher.MatchesFile(ToPlatformPath("src/obj"), "included.cs").ShouldBeFalse();
    }

    [Theory]
    [InlineData("*")]
    [InlineData("*.*")]
    public void TerminalGlobstarAllFilesExcludePrunesSubtree(string filePattern)
    {
        MSBuildPathMatcher matcher = new(ToPlatformPath("**/obj/**"), filePattern);

        matcher.MatchesDirectory(ReadOnlySpan<char>.Empty, ToPlatformPath("src/obj"))
            .ShouldBe(DirectoryMatchType.AllDescendantFilesMatch);
        matcher.MatchesDirectory(ToPlatformPath("src/obj"), "deep")
            .ShouldBe(DirectoryMatchType.AllDescendantFilesMatch);
    }

    [Fact]
    public void AllFilesIncludeReportsAllDescendantFilesMatch()
    {
        MSBuildPathMatcher matcher = new(ToPlatformPath("**/obj/**"), "*.*");

        matcher.MatchesDirectory(ReadOnlySpan<char>.Empty, ToPlatformPath("src/obj"))
            .ShouldBe(DirectoryMatchType.AllDescendantFilesMatch);
    }

    [Fact]
    public void FixedDepthPatternDoesNotRecursePastMatch()
    {
        MSBuildPathMatcher matcher = new(ToPlatformPath("src/*"), "*.cs");

        matcher.MatchesDirectory(ToPlatformPath("src"), "one")
            .ShouldBe(DirectoryMatchType.MayContainMatchingFiles);
        matcher.MatchesDirectory(ToPlatformPath("src/one"), "deep")
            .ShouldBe(DirectoryMatchType.NoDescendantFilesMatch);
    }

    [Fact]
    public void NoWildcardDirectoryMatchesOnlyEnumerationRoot()
    {
        MSBuildPathMatcher matcher = new(string.Empty, "*.cs");

        matcher.MatchesFile(ReadOnlySpan<char>.Empty, "root.cs").ShouldBeTrue();
        matcher.MatchesFile("sub", "nested.cs").ShouldBeFalse();
        matcher.CanMatchDescendants(ReadOnlySpan<char>.Empty).ShouldBeFalse();
    }

    [Fact]
    public void TerminalGlobstarCanAlwaysMatchDescendants()
    {
        MSBuildPathMatcher matcher = new("**", "*.cs");

        matcher.CanMatchDescendants(ReadOnlySpan<char>.Empty).ShouldBeTrue();
        matcher.CanMatchDescendants(ToPlatformPath("one/two")).ShouldBeTrue();
    }

    [Fact]
    public void FilesystemMatchedFilenameUsesCaseSensitiveComparison()
    {
        MSBuildPathMatcher matcher = new(
            "**",
            "*.CS",
            filesystemCaseSensitive: true,
            matchFileNameInternally: false);

        matcher.MatchesFile("src", "source.cs").ShouldBeFalse();
        matcher.MatchesFile("src", "source.CS").ShouldBeTrue();
    }

    [Fact]
    public void InternallyMatchedFilenameUsesCaseInsensitiveComparison()
    {
        MSBuildPathMatcher matcher = new(
            "**/generated",
            "*.CS",
            filesystemCaseSensitive: true,
            matchFileNameInternally: true);

        matcher.MatchesFile(ToPlatformPath("src/generated"), "source.cs").ShouldBeTrue();
    }

    [Fact]
    public void DirectorySegmentsBecomeCaseInsensitiveAfterGlobstar()
    {
        MSBuildPathMatcher beforeGlobstar = new(
            ToPlatformPath("SRC/**"),
            "*.cs",
            filesystemCaseSensitive: true,
            matchFileNameInternally: true);
        MSBuildPathMatcher afterGlobstar = new(
            ToPlatformPath("**/GENERATED"),
            "*.cs",
            filesystemCaseSensitive: true,
            matchFileNameInternally: true);

        beforeGlobstar.MatchesDirectory(ReadOnlySpan<char>.Empty, "src")
            .ShouldBe(DirectoryMatchType.NoDescendantFilesMatch);
        afterGlobstar.MatchesDirectory("src", "generated")
            .ShouldBe(DirectoryMatchType.MayContainMatchingFiles);
    }

    [Fact]
    public void FilesystemFilteredStarDotStarDoesNotMatchExtensionlessFile()
    {
        MSBuildPathMatcher matcher = new(
            "**",
            "*.*",
            filesystemCaseSensitive: true,
            matchFileNameInternally: false,
            treatStarDotStarAsAllFiles: false);

        matcher.MatchesFile("src", "README").ShouldBeFalse();
        matcher.MatchesFile("src", "source.cs").ShouldBeTrue();
    }

    [Fact]
    public void StarDotStarExcludeCanCoverExtensionlessFile()
    {
        MSBuildPathMatcher matcher = new(
            "**",
            "*.*",
            filesystemCaseSensitive: true,
            matchFileNameInternally: true,
            treatStarDotStarAsAllFiles: true);

        matcher.MatchesFile("src", "README").ShouldBeTrue();
        matcher.MatchesDirectory(ReadOnlySpan<char>.Empty, "src")
            .ShouldBe(DirectoryMatchType.AllDescendantFilesMatch);
    }

    [Fact]
    public void FilesystemFilteredTrailingDotDoesNotUseWindowsCompatibilityRegex()
    {
        MSBuildPathMatcher matcher = new(
            "**",
            "*.",
            filesystemCaseSensitive: true,
            matchFileNameInternally: false,
            useTrailingDotCompatibility: false);

        matcher.MatchesFile("src", "README").ShouldBeFalse();
        matcher.MatchesFile("src", "README.").ShouldBeTrue();
    }

    [WindowsOnlyTheory]
    [InlineData("LICENSE.*")]
    [InlineData("LICE*.*")]
    public void WindowsDirectoryPatternUsesDosWildcardSemantics(string directoryPattern)
    {
        MSBuildPathMatcher matcher = new(
            ToPlatformPath($"{directoryPattern}/**"),
            "*.cs",
            useWin32DirectoryMatch: true);

        matcher.MatchesDirectory(ReadOnlySpan<char>.Empty, "LICENSE")
            .ShouldBe(DirectoryMatchType.MayContainMatchingFiles);
    }

    [UnixOnlyFact]
    public void CandidatePathsTreatBackslashAsNameCharacter()
    {
        MSBuildPathMatcher directoryMatcher = new("*", "*.cs");
        MSBuildPathMatcher fileMatcher = new("**", "b.cs");

        directoryMatcher.MatchesDirectory(ReadOnlySpan<char>.Empty, @"a\b")
            .ShouldBe(DirectoryMatchType.MayContainMatchingFiles);
        fileMatcher.MatchesFileName(@"a\b.cs").ShouldBeFalse();
    }

    [Fact]
    public void CultureSensitiveMatchingUsesRegexCaseFolding()
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            MSBuildPathMatcher directoryMatcher = new(
                ToPlatformPath("**/\u200B"),
                "*.cs",
                preserveLegacySemantics: true);
            MSBuildPathMatcher fileMatcher = new(
                ToPlatformPath("**/a"),
                "\u200B.cs",
                preserveLegacySemantics: true);

            directoryMatcher.MatchesFile("\u00AD", "source.cs").ShouldBeFalse();
            fileMatcher.MatchesFile("a", "\u00AD.cs").ShouldBeFalse();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void LegacyRegexSemanticsDependOnCurrentCulture()
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            MSBuildPathMatcher invariantMatcher = new(
                ToPlatformPath("**/I"),
                "*.cs",
                preserveLegacySemantics: true);
            bool[] invariantMatches = GetMatches(invariantMatcher);

            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            MSBuildPathMatcher englishMatcher = new(
                ToPlatformPath("**/I"),
                "*.cs",
                preserveLegacySemantics: true);
            bool[] englishMatches = GetMatches(englishMatcher);

            Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
            MSBuildPathMatcher turkishMatcher = new(
                ToPlatformPath("**/I"),
                "*.cs",
                preserveLegacySemantics: true);
            bool[] turkishMatches = GetMatches(turkishMatcher);

            invariantMatches.ShouldBe([true, false, false]);
            englishMatches.ShouldBe([true, true, false]);
            turkishMatches.ShouldBe([false, false, true]);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        static bool[] GetMatches(MSBuildPathMatcher matcher) =>
        [
            matcher.MatchesFile("i", "source.cs"),
            matcher.MatchesFile("İ", "source.cs"),
            matcher.MatchesFile("ı", "source.cs"),
        ];
    }

    [Fact]
    public void CultureSensitiveQuestionMarkMatchesLegacyRegexSemantics()
    {
        MSBuildPathMatcher regular = new("**", "?", preserveLegacySemantics: true);
        MSBuildPathMatcher trailingDot = new("**", "?.", preserveLegacySemantics: true);

        regular.MatchesFile(ReadOnlySpan<char>.Empty, "\n").ShouldBeFalse();
        trailingDot.MatchesFile(ReadOnlySpan<char>.Empty, "a").ShouldBeFalse();
        trailingDot.MatchesFile(ReadOnlySpan<char>.Empty, "ab").ShouldBeTrue();
        trailingDot.MatchesFile(ReadOnlySpan<char>.Empty, "a\n").ShouldBeFalse();
    }

    [Theory]
    [InlineData("*.cs", "Program.CS", true)]
    [InlineData("*.cs", "Program.txt", false)]
    [InlineData("*a*b?.cs", "aaabbbc.cs", true)]
    [InlineData("*a*b?.cs", "aaabb.cs", true)]
    [InlineData("*a*b?.cs", "aaac.cs", false)]
    [InlineData("[a]+(b).cs", "[A]+(B).CS", true)]
    [InlineData("*", "", true)]
    [InlineData("?", "", false)]
    [InlineData("K.cs", "\u212A.cs", true)]
    [InlineData("S.cs", "\u017F.cs", false)]
    [InlineData("?.cs", "\n.cs", false)]
    [InlineData("?.cs", "a.cs\n", true)]
    [InlineData("a*.cs", "a\nb.cs", true)]
    [InlineData("a*.*", "abc", true)]
    [InlineData("*.", "README", true)]
    [InlineData("?.", "ab", true)]
    public void InvariantNameMatchingPreservesRegexSemantics(string pattern, string name, bool expected)
    {
        MSBuildPathMatcher matcher = new(
            "**",
            pattern,
            preserveLegacySemantics: true,
            useInvariantCulture: true);

        matcher.MatchesFileName(name.AsSpan()).ShouldBe(expected);
    }

    [Theory]
    [InlineData("GENERATED", "generated", true)]
    [InlineData("*a*b?", "aaabbbc", true)]
    [InlineData("*a*b?", "aaac", false)]
    [InlineData("[a]+(b)", "[A]+(B)", true)]
    [InlineData("K", "\u212A", true)]
    [InlineData("?", "\n", false)]
    [InlineData("?", "a\n", true)]
    [InlineData("a*b", "a\nb", true)]
    public void InvariantDirectoryMatchingPreservesRegexSemantics(string pattern, string name, bool expected)
    {
        MSBuildPathMatcher matcher = new(
            ToPlatformPath($"**/{pattern}"),
            "*.cs",
            preserveLegacySemantics: true,
            useInvariantCulture: true);

        matcher.MatchesFilesInDirectory(name).ShouldBe(expected);
    }

    [Fact]
    public void UnicodeMatchingDoesNotChangeSubsequentAsciiMatching()
    {
        MSBuildPathMatcher matcher = new(
            ToPlatformPath("**/GENERATED"),
            "*.cs",
            preserveLegacySemantics: true,
            useInvariantCulture: true);

        matcher.MatchesFile("generated", "source.cs").ShouldBeTrue();
        matcher.MatchesFile("generated", "\u00E9.cs").ShouldBeTrue();
        matcher.MatchesFile("\u00E9", "source.cs").ShouldBeFalse();
        matcher.MatchesFile("generated", "source.cs").ShouldBeTrue();
    }

    [Theory]
    [InlineData("?")]
    [InlineData("*a")]
    [InlineData("*?a")]
    [InlineData("a*b?*")]
    [InlineData("*a*b?")]
    [InlineData("[a]+(b)")]
    [InlineData("K*")]
    [InlineData("a*.*")]
    [InlineData("*.")]
    public void InvariantNameMatchingAgreesWithRegex(string pattern)
    {
        MSBuildPathMatcher matcher = new(
            "**",
            pattern,
            preserveLegacySemantics: true,
            useInvariantCulture: true);
        Regex regex = new(
            GlobbingRegex.RegularExpressionFromFileSpec(string.Empty, string.Empty, pattern),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        string[] names =
        [
            "", "a", "A", "b", "ab", "aa", "ba", "aab", "abb", "aaabbbc",
            "a.b", "a.", "[A]+(B)", "K", "\u212A", "\u00E9", "a\u00E9b",
            "\n", "a\n", "\na", "a\nb", "\r", "a\rb",
        ];

        foreach (string name in names)
        {
            matcher.MatchesFileName(name.AsSpan()).ShouldBe(regex.IsMatch(name), $"pattern: {pattern}, name: {name}");
        }
    }

    [Fact]
    public void GlobMatchingAndCapturesAgreeWithLegacyRegex()
    {
        string[] patterns =
        [
            "", "*", "?", "*.*", "*.cs", "*.", "?.", "a*.*", "a?.",
            "**/*.cs", "**/a/*.cs", "**/a/**/b/*.cs", "a/*/b/?.cs",
            "a/**/b/**/c/*.", "**/I/*.cs", "a//./**/**/b/*.cs",
            "**/a/*.", "**/*/?.", "*/?.cs", "a/**/*.", @"a\**\*.", "a/**/?.",
        ];
        string[] names =
        [
            "", "a", "ab", "a.cs", "a.CS", "a.cs\n", "a\n.cs",
            "a/b.cs", "a/a/b.cs", "a/b/c.cs", "a/x/b/q.cs", "a/b/c/README",
            "a//b.cs", @"a\b.cs", @"a/b\c.cs", @"a\b/c.cs",
            "i/source.cs", "I/source.cs", "İ/source.cs", "ı/source.cs",
            "a/é.cs", "a/\n/b.cs", "a/\r/b.cs", "a/b/ab", "a/b/c/ab",
        ];

        foreach (string pattern in patterns)
        {
            FileMatcher.Default.GetFileSpecInfo(pattern, out string fixedPart, out string wildcardPart,
                out string filePart, out _, out bool legal);
            legal.ShouldBeTrue();
            Regex regex = new(GlobbingRegex.RegularExpressionFromFileSpec(fixedPart, wildcardPart, filePart),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            GlobPattern matcher = new(fixedPart, wildcardPart, filePart, useInvariantCulture: true);
            foreach (string name in names)
            {
                Match expected = regex.Match(name);
                matcher.GetMatchInfo(name, out bool matched, out string wildcardDirectory, out string filename);
                string context = $"pattern: {pattern}, name: {name}";
                matcher.IsMatch(name).ShouldBe(expected.Success, context);
                matched.ShouldBe(expected.Success, context);
                wildcardDirectory.ShouldBe(expected.Groups["WILDCARDDIR"].Value, context);
                filename.ShouldBe(expected.Groups["FILENAME"].Value, context);
            }
        }
    }

    [Fact]
    public void GeneratedPathsPreserveMatchingAndCaptureSemantics()
    {
        (string Pattern, string MatchingPath)[] fixedDirectories =
        [
            ("", ""), ("a/", "a/"), ("a//./", "a/"), ("/", "/"),
        ];
        (string Pattern, string MatchingPath)[] directories =
        [
            ("", ""), ("**/", ""), ("**/", "deep/nested/"),
            ("**/a/", "a/"), ("**/a/", "deep/a/"), ("*/", "dir/"),
            ("a/**/", "a/"), ("a/**/", "a/deep/nested/"),
            ("**/a/**/b/", "a/b/"), ("**/a/**/b/", "deep/a/nested/b/"),
            ("?/**/?/", "a/b/"), ("?/**/?/", "a/nested/b/"),
        ];
        (string Pattern, string MatchingPath)[] filenames =
        [
            ("*", "source.cs"), ("?", "a"), ("*.cs", "source.cs"),
            ("*.*", "source.cs"), ("*.*", "README"), ("*.", "README"),
            ("?.", "ab"), ("*a?b*", "prefixacbsuffix"), ("a*.*", "abc.cs"),
        ];
        char[] characters = ['a', 'A', 'b', 'c', 's', '.', '/', '\\', '\n', '\r', '\u00E9'];
        Random random = new(1729);
        string[] paths = new string[512];
        for (int i = 0; i < paths.Length; i++)
        {
            char[] path = new char[random.Next(20)];
            for (int j = 0; j < path.Length; j++)
            {
                path[j] = characters[random.Next(characters.Length)];
            }
            paths[i] = new string(path);
        }

        foreach ((string fixedDirectory, string fixedInput) in fixedDirectories)
        {
            foreach ((string directory, string directoryInput) in directories)
            {
                foreach ((string filename, string filenameInput) in filenames)
                {
                    GlobPattern matcher = new(fixedDirectory, directory, filename, useInvariantCulture: true);
                    Regex regex = new(GlobbingRegex.RegularExpressionFromFileSpec(fixedDirectory, directory, filename),
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    string matchingPath = fixedInput + directoryInput + filenameInput;
                    regex.IsMatch(matchingPath).ShouldBeTrue($"positive case for {fixedDirectory}{directory}{filename}");
                    AssertMatchesReference(matchingPath);
                    foreach (string path in paths)
                    {
                        AssertMatchesReference(path);
                    }

                    void AssertMatchesReference(string path)
                    {
                        Match expected = regex.Match(path);
                        matcher.GetMatchInfo(path, out bool matched, out string wildcardMatch, out string filenameMatch);
                        string context = $"glob: {fixedDirectory}{directory}{filename}, path: {path}";
                        matcher.IsMatch(path).ShouldBe(expected.Success, context);
                        matched.ShouldBe(expected.Success, context);
                        wildcardMatch.ShouldBe(expected.Groups["WILDCARDDIR"].Value, context);
                        filenameMatch.ShouldBe(expected.Groups["FILENAME"].Value, context);
                    }
                }
            }
        }
    }

    [Fact]
    public void LongPatternsPreserveCapturesWhenBuffersGrow()
    {
        string[] patterns = new string[96];
        string[] directories = new string[96];
        Array.Fill(patterns, "a*");
        Array.Fill(directories, "abc");
        string wildcard = string.Join("/", patterns) + "/";
        string directory = string.Join("/", directories) + "/";
        string path = directory + "file.cs";
        GlobPattern matcher = new("", wildcard, "*.cs", useInvariantCulture: true);

        for (int i = 0; i < 2; i++)
        {
            matcher.IsMatch(path).ShouldBeTrue();
            matcher.GetMatchInfo(path, out bool matched, out string wildcardMatch, out string filenameMatch);
            matched.ShouldBeTrue();
            wildcardMatch.ShouldBe(directory);
            filenameMatch.ShouldBe("file.cs");
        }
    }

    [Theory]
    [InlineData(@"**\**\a\", @"b\a\file.cs")]
    [InlineData(@"**\**/a\", @"b/a\file.cs")]
    [InlineData(@"a\**\**\", @"a\file.cs")]
    [InlineData(@"a\**\**\", @"a\b\file.cs")]
    public void RawGlobPartsPreserveMixedRecursiveSeparators(string wildcard, string path)
    {
        GlobPattern matcher = new("", wildcard, "*.cs", useInvariantCulture: true);
        Regex regex = new(GlobbingRegex.RegularExpressionFromFileSpec("", wildcard, "*.cs"),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Match expected = regex.Match(path);
        expected.Success.ShouldBeTrue();
        matcher.IsMatch(path).ShouldBeTrue();
        matcher.GetMatchInfo(path, out bool matched, out string wildcardMatch, out string filenameMatch);
        matched.ShouldBeTrue();
        wildcardMatch.ShouldBe(expected.Groups["WILDCARDDIR"].Value);
        filenameMatch.ShouldBe(expected.Groups["FILENAME"].Value);
    }

    [Fact]
    public void FixedDirectoryConsumesRepeatedSeparatorsBeforeRecursiveCapture()
    {
        GlobPattern matcher = new("root/", "**/", "*.cs", useInvariantCulture: true);

        matcher.GetMatchInfo("root///file.cs", out bool matched, out string wildcard, out string filename);
        matched.ShouldBeTrue();
        wildcard.ShouldBeEmpty();
        filename.ShouldBe("file.cs");
    }

    [Theory]
    [InlineData("")]
    [InlineData("en-US")]
    [InlineData("tr-TR")]
    public void UnicodeNameMatchingAgreesWithRegex(string cultureName)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            string[] patterns = ["I", "i", "İ", "ı", "K", "K", "S", "ſ", "Σ", "σ", "ς", "Å", "Å", "Ǆ", "ǅ", "ǆ"];
            foreach (string pattern in patterns)
            {
                GlobPattern matcher = GlobPattern.ForName(pattern, ignoreCase: true, useInvariantCulture: false, isFilePattern: false);
                Regex regex = new(Regex.Escape(pattern), RegexOptions.IgnoreCase);
                foreach (string name in patterns)
                {
                    matcher.IsMatch(name).ShouldBe(regex.IsMatch(name), $"culture: {cultureName}, pattern: {pattern}, name: {name}");
                }
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    private static string ToPlatformPath(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}
