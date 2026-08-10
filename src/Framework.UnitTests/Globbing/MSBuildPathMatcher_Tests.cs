// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
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
            useTrailingDotRegex: false);

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
                useCultureSensitiveMatch: true);
            MSBuildPathMatcher fileMatcher = new(
                ToPlatformPath("**/a"),
                "\u200B.cs",
                useCultureSensitiveMatch: true);

            directoryMatcher.MatchesFile("\u00AD", "source.cs").ShouldBeFalse();
            fileMatcher.MatchesFile("a", "\u00AD.cs").ShouldBeFalse();
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void CultureSensitiveQuestionMarkMatchesLegacyRegexSemantics()
    {
        MSBuildPathMatcher regular = new("**", "?", useCultureSensitiveMatch: true);
        MSBuildPathMatcher trailingDot = new("**", "?.", useCultureSensitiveMatch: true);

        regular.MatchesFile(ReadOnlySpan<char>.Empty, "\n").ShouldBeFalse();
        trailingDot.MatchesFile(ReadOnlySpan<char>.Empty, "a").ShouldBeFalse();
        trailingDot.MatchesFile(ReadOnlySpan<char>.Empty, "ab").ShouldBeTrue();
        trailingDot.MatchesFile(ReadOnlySpan<char>.Empty, "a\n").ShouldBeFalse();
    }

    private static string ToPlatformPath(string path) => path.Replace('/', Path.DirectorySeparatorChar);
}