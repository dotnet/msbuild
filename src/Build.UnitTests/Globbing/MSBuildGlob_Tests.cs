// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Globbing;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.Globbing
{
    /// <summary>
    ///     Actual parsing tests are covered by FileMatcher_Tests (e.g. FileMatcher_Tests.SplitFileSpec)/>
    /// </summary>
    [TestClass]
    public class MSBuildGlob_Tests
    {
        [MSBuildTestMethod]
        [DataRow("")]
        [DataRow("a")]
#if TEST_ISWINDOWS
        [DataRow(@"c:\a")]
#else
        [DataRow("/a")]
#endif
        public void GlobRootEndsWithTrailingSlash(string globRoot)
        {
            var glob = MSBuildGlob.Parse(globRoot, "*");

            Assert.AreEqual(glob.TestOnlyGlobRoot.LastOrDefault(), Path.DirectorySeparatorChar);
        }

        [MSBuildTestMethod]
        public void GlobFromAbsoluteRootDoesNotChangeTheRoot()
        {
            var globRoot = NativeMethodsShared.IsWindows ? @"c:\a" : "/a";
            var glob = MSBuildGlob.Parse(globRoot, "*");

            var expectedRoot = Path.Combine(Directory.GetCurrentDirectory(), globRoot).WithTrailingSlash();
            Assert.AreEqual(expectedRoot, glob.TestOnlyGlobRoot);
        }

        [MSBuildTestMethod]
        public void GlobFromEmptyRootUsesCurrentDirectory()
        {
            var glob = MSBuildGlob.Parse(string.Empty, "*");

            Assert.AreEqual(Directory.GetCurrentDirectory().WithTrailingSlash(), glob.TestOnlyGlobRoot);
        }

        [MSBuildTestMethod]
        public void GlobFromNullRootThrows()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => MSBuildGlob.Parse(null, "*"));
        }

        [MSBuildTestMethod]
        public void GlobFromRelativeGlobRootNormalizesRootAgainstCurrentDirectory()
        {
            var globRoot = "a/b/c";
            var glob = MSBuildGlob.Parse(globRoot, "*");

            var expectedRoot = NormalizeRelativePathForGlobRepresentation(globRoot);
            Assert.AreEqual(expectedRoot, glob.TestOnlyGlobRoot);
        }

        [MSBuildTestMethod]
        public void GlobFromRootWithInvalidPathThrows()
        {
            for (int i = 0; i < 128; i++)
            {
                if (FileUtilities.InvalidPathChars.Contains((char)i))
                {
                    Assert.ThrowsExactly<ArgumentException>(() => MSBuildGlob.Parse(((char)i).ToString(), "*"));
                }
            }
        }

        [MSBuildTestMethod]
        [DataRow(
            "a/b/c",
            "**",
            "a/b/c")]
        [DataRow(
            "a/b/c",
            "../../**",
            "a")]
        [DataRow(
            "a/b/c",
            "../d/e/**",
            "a/b/d/e")]
        public void GlobWithRelativeFixedDirectoryPartShouldMismatchTheGlobRoot(string globRoot, string filespec, string expectedFixedDirectoryPart)
        {
            var glob = MSBuildGlob.Parse(globRoot, filespec);

            var expectedFixedDirectory = NormalizeRelativePathForGlobRepresentation(expectedFixedDirectoryPart);

            Assert.AreEqual(expectedFixedDirectory, glob.FixedDirectoryPart);
        }

        [MSBuildTestMethod]
        public void GlobFromNullFileSpecThrows()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() => MSBuildGlob.Parse(null));
        }

        // Smoke test. Comprehensive parsing tests in FileMatcher

        [MSBuildTestMethod]
        public void GlobParsingShouldInitializeState()
        {
            var globRoot = NativeMethodsShared.IsWindows ? @"c:\a" : "/a";
            var fileSpec = $"b/**/*.cs";
            var glob = MSBuildGlob.Parse(globRoot, fileSpec);

            var expectedFixedDirectory = Path.Combine(globRoot, "b").WithTrailingSlash();

            Assert.IsTrue(glob.IsLegal);
            Assert.IsTrue(glob.TestOnlyNeedsRecursion);
            Assert.AreEqual(fileSpec, glob.TestOnlyFileSpec);

            Assert.AreEqual(globRoot.WithTrailingSlash(), glob.TestOnlyGlobRoot);
            Assert.StartsWith(glob.TestOnlyGlobRoot, glob.FixedDirectoryPart);

            Assert.AreEqual(expectedFixedDirectory, glob.FixedDirectoryPart);
            Assert.AreEqual("**/", glob.WildcardDirectoryPart);
            Assert.AreEqual("*.cs", glob.FilenamePart);
        }

        [MSBuildTestMethod]
        public void GlobParsingShouldInitializePartialStateOnIllegalFileSpec()
        {
            var globRoot = NativeMethodsShared.IsWindows ? @"c:\a" : "/a";
            var illegalFileSpec = $"b/.../**/*.cs";
            var glob = MSBuildGlob.Parse(globRoot, illegalFileSpec);

            Assert.IsFalse(glob.IsLegal);
            Assert.IsFalse(glob.TestOnlyNeedsRecursion);
            Assert.AreEqual(illegalFileSpec, glob.TestOnlyFileSpec);

            Assert.AreEqual(globRoot.WithTrailingSlash(), glob.TestOnlyGlobRoot);

            Assert.AreEqual(string.Empty, glob.FixedDirectoryPart);
            Assert.AreEqual(string.Empty, glob.WildcardDirectoryPart);
            Assert.AreEqual(string.Empty, glob.FilenamePart);

            Assert.IsFalse(glob.IsMatch($"b/.../c/d.cs"));
        }

        [MSBuildTestMethod]
        public void GlobParsingShouldDeduplicateRegexes()
        {
            var globRoot = NativeMethodsShared.IsWindows ? @"c:\a" : "/a";
            var fileSpec = $"b/**/*.cs";
            var glob1 = MSBuildGlob.Parse(globRoot, fileSpec);
            var glob2 = MSBuildGlob.Parse(globRoot, fileSpec);

            Assert.AreSame(glob1.TestOnlyRegex, glob2.TestOnlyRegex);
        }

        [MSBuildTestMethod]
        public void GlobIsNotUnescaped()
        {
            var glob = MSBuildGlob.Parse("%42/%42");

            Assert.IsTrue(glob.IsLegal);
            Assert.EndsWith("%42" + Path.DirectorySeparatorChar, glob.FixedDirectoryPart);
            Assert.AreEqual(string.Empty, glob.WildcardDirectoryPart);
            Assert.AreEqual("%42", glob.FilenamePart);
        }

        [MSBuildTestMethod]
        public void GlobMatchingShouldThrowOnNullMatchArgument()
        {
            var glob = MSBuildGlob.Parse("*");

            Assert.ThrowsExactly<ArgumentNullException>(() => glob.IsMatch(null));
        }

        [MSBuildTestMethod]
        public void GlobMatchShouldReturnFalseIfArgumentContainsInvalidPathOrFileCharacters()
        {
            var glob = MSBuildGlob.Parse("*");

            for (int i = 0; i < 128; i++)
            {
                if (FileUtilities.InvalidPathChars.Contains((char)i))
                {
                    Assert.IsFalse(glob.IsMatch(((char)i).ToString()));
                }
            }

            foreach (var invalidFileChar in FileUtilities.InvalidFileNameCharsArray)
            {
                if (invalidFileChar == '\\' || invalidFileChar == '/')
                {
                    continue;
                }

                Assert.IsFalse(glob.IsMatch(invalidFileChar.ToString()));
            }
        }

        [MSBuildTestMethod]
        public void GlobMatchingBehaviourWhenInputIsASingleSlash()
        {
            var glob = MSBuildGlob.Parse("*");

            if (NativeMethodsShared.IsUnixLike)
            {
                // \ is an acceptable file character on Unix, * should match it
                Assert.IsTrue(glob.IsMatch("\\"));

                // / means root on Unix
                Assert.IsFalse(glob.IsMatch("/"));
            }
            else
            {
                // \ means partition root on Windows
                Assert.IsFalse(glob.IsMatch("\\"));

                // / also means partition root on Windows
                Assert.IsFalse(glob.IsMatch("/"));
            }
        }

        [MSBuildTestMethod]
        public void GlobMatchingShouldWorkWithLiteralStrings()
        {
            var glob = MSBuildGlob.Parse("abc");

            Assert.IsTrue(glob.IsMatch("abc"));
        }

        // Just a smoke test. Comprehensive tests in FileMatcher_Tests

        [MSBuildTestMethod]
        public void GlobMatchingShouldWorkWithWildcards()
        {
            var glob = MSBuildGlob.Parse("ab?c*");

            Assert.IsFalse(glob.IsMatch("acd"));
        }

        [MSBuildTestMethod]
        public void GlobMatchingShouldNotUnescapeInput()
        {
            var glob = MSBuildGlob.Parse("%42");

            Assert.IsFalse(glob.IsMatch("B"));
        }

        [MSBuildTestMethod]
        [DataRow("")]
        [DataRow("a/b/c")]
        public void GlobShouldMatchEmptyArgWhenGlobIsEmpty(string globRoot)
        {
            var glob = MSBuildGlob.Parse(globRoot, string.Empty);

            Assert.IsTrue(glob.IsMatch(string.Empty));
        }

        [MSBuildTestMethod]
        [DataRow("")]
        [DataRow("a/b/c")]
        public void GlobShouldMatchEmptyArgWhenGlobAcceptsEmptyString(string globRoot)
        {
            var glob = MSBuildGlob.Parse(globRoot, "*");

            Assert.IsTrue(glob.IsMatch(string.Empty));
        }

        [MSBuildTestMethod]
        [DataRow("")]
        [DataRow("a/b/c")]
        public void GlobShouldNotMatchEmptyArgWhenGlobDoesNotRepresentEmpty(string globRoot)
        {
            var glob = MSBuildGlob.Parse(globRoot, "*a*");

            Assert.IsFalse(glob.IsMatch(string.Empty));
        }

        [MSBuildTestMethod]
        [Ignore("TODO")]
        public void GlobMatchingShouldTreatIllegalFileSpecAsLiteral()
        {
            var illegalSpec = "|...*";
            var glob = MSBuildGlob.Parse(illegalSpec);

            Assert.IsFalse(glob.IsLegal);
            Assert.IsTrue(glob.IsMatch(illegalSpec));
        }

        public static IEnumerable<object[]> GlobMatchingShouldRespectTheRootOfTheGlobTestData => GlobbingTestData.GlobbingConesTestData;

        [MSBuildTestMethod]

        [DynamicData(nameof(GlobMatchingShouldRespectTheRootOfTheGlobTestData))]

        public void GlobMatchingShouldRespectTheRootOfTheGlob(string fileSpec, string stringToMatch, string globRoot, bool shouldMatch)
        {
            var glob = MSBuildGlob.Parse(globRoot, fileSpec);

            if (shouldMatch)
            {
                Assert.IsTrue(glob.IsMatch(stringToMatch));
            }
            else
            {
                Assert.IsFalse(glob.IsMatch(stringToMatch));
            }
        }

        private static string NormalizeRelativePathForGlobRepresentation(string expectedFixedDirectoryPart)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), expectedFixedDirectoryPart).Replace("/", "\\").WithTrailingSlash();
        }

        [MSBuildTestMethod]
        public void GlobMatchingShouldWorkWithComplexRelativeLiterals()
        {
            var glob = MSBuildGlob.Parse("u/x", "../../u/x/d11/d21/../d22/../../d12/a.cs");

            Assert.IsTrue(glob.IsMatch(@"../x/d13/../../x/d12/d23/../a.cs"));
            Assert.IsFalse(glob.IsMatch(@"../x/d13/../x/d12/d23/../a.cs"));
        }

        [MSBuildTestMethod]
        [DataRow(
            @"a/b\c",
            @"d/e\f/**\a.cs",
            @"d\e/f\g/h\i/a.cs",
            @"d\e/f\", @"g/h\i/", @"a.cs")]
        [DataRow(
            @"a/b\c",
            @"d/e\f/*b*\*.cs",
            @"d\e/f\abc/a.cs",
            @"d\e/f\", @"abc/", @"a.cs")]
        [DataRow(
            @"a/b/\c",
            @"d/e\/*b*/\*.cs",
            @"d\e\\abc/\a.cs",
            @"d\e\\", @"abc\\", @"a.cs")]
        public void GlobMatchingIgnoresSlashOrientationAndRepetitions(string globRoot, string fileSpec, string stringToMatch,
            string fixedDirectoryPart, string wildcardDirectoryPart, string filenamePart)
        {
            var glob = MSBuildGlob.Parse(globRoot, fileSpec);

            Assert.IsTrue(glob.IsMatch(stringToMatch));

            MSBuildGlob.MatchInfoResult result = glob.MatchInfo(stringToMatch);
            Assert.IsTrue(result.IsMatch);

            string NormalizeSlashes(string path)
            {
                string normalizedPath = path.Replace(Path.DirectorySeparatorChar == '/' ? '\\' : '/', Path.DirectorySeparatorChar);
                return NativeMethodsShared.IsWindows ? normalizedPath.Replace("\\\\", "\\") : normalizedPath;
            }

            var rootedFixedDirectoryPart = Path.Combine(FileUtilities.NormalizePath(globRoot), fixedDirectoryPart);

            Assert.AreEqual(FileUtilities.GetFullPathNoThrow(rootedFixedDirectoryPart), result.FixedDirectoryPartMatchGroup);
            Assert.AreEqual(NormalizeSlashes(wildcardDirectoryPart), result.WildcardDirectoryPartMatchGroup);
            Assert.AreEqual(NormalizeSlashes(filenamePart), result.FilenamePartMatchGroup);
        }
    }
}
