// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Shouldly;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public class AbsolutePath_Tests
    {
        private static AbsolutePath GetTestBasePath()
        {
            string baseDirectory = Path.Combine(Path.GetTempPath(), "abspath_test_base");
            return new AbsolutePath(baseDirectory, ignoreRootedCheck: false);
        }

        private static void ValidatePathAcceptance(string path, bool shouldBeAccepted)
        {
            if (shouldBeAccepted)
            {
                // Should not throw - these are truly absolute paths
                var absolutePath = new AbsolutePath(path);
                absolutePath.Value.ShouldBe(path);
            }
            else
            {
                // Should throw ArgumentException for any non-absolute path
                Should.Throw<ArgumentException>(() => new AbsolutePath(path, ignoreRootedCheck: false),
                    $"Path '{path}' should be rejected as it's not a true absolute path");
            }
        }

        [MSBuildTestMethod]
        public void AbsolutePath_FromAbsolutePath_ShouldPreservePath()
        {
            string absolutePathString = Path.GetTempPath();
            var absolutePath = new AbsolutePath(absolutePathString);

            absolutePath.Value.ShouldBe(absolutePathString);
            Path.IsPathRooted(absolutePath.Value).ShouldBeTrue();
        }

        [MSBuildTestMethod]
        public void AbsolutePath_NullOrEmpty_ShouldThrowOnNull()
        {
            string? path = null;

            Should.Throw<ArgumentNullException>(() => new AbsolutePath(path!));
        }

        [MSBuildTestMethod]
        [UseInvariantCulture]
        public void AbsolutePath_NullOrEmpty_ShouldThrowOnEmpty()
        {
            string path = "";

            var exception = Should.Throw<ArgumentException>(() => new AbsolutePath(path));
            exception.Message.ShouldStartWith("The value cannot be an empty string.");
        }

        [MSBuildTestMethod]
        public void AbsolutePath_NullOrEmptyWithBasePath_ShouldThrowOnNull()
        {
            string? path = null;
            var basePath = GetTestBasePath();

            Should.Throw<ArgumentNullException>(() => new AbsolutePath(path!, basePath));
        }

        [MSBuildTestMethod]
        [UseInvariantCulture]
        public void AbsolutePath_NullOrEmptyWithBasePath_ShouldThrowOnEmpty()
        {
            string path = "";
            var basePath = GetTestBasePath();

            var exception = Should.Throw<ArgumentException>(() => new AbsolutePath(path, basePath));
            exception.Message.ShouldStartWith("The value cannot be an empty string.");
        }

        [MSBuildTestMethod]
        [DataRow("subfolder")]
        [DataRow("deep/nested/path")]
        [DataRow(".")]
        [DataRow("..")]
        public void AbsolutePath_FromRelativePath_ShouldResolveAgainstBase(string relativePath)
        {
            string baseDirectory = Path.Combine(Path.GetTempPath(), "testfolder");
            var basePath = new AbsolutePath(baseDirectory);
            var absolutePath = new AbsolutePath(relativePath, basePath);

            Path.IsPathRooted(absolutePath.Value).ShouldBeTrue();

            string expectedPath = Path.Combine(baseDirectory, relativePath);
            absolutePath.Value.ShouldBe(expectedPath);
        }

        [MSBuildTestMethod]
        public void AbsolutePath_Equality_ShouldWorkCorrectly()
        {
            string testPath = Path.GetTempPath();
            var path1 = new AbsolutePath(testPath);
            var path2 = new AbsolutePath(testPath);
            var differentPath = new AbsolutePath(Path.Combine(testPath, "different"));

            path1.ShouldBe(path2);
            (path1 == path2).ShouldBeTrue();
            path1.ShouldNotBe(differentPath);
            (path1 == differentPath).ShouldBeFalse();
        }

        [MSBuildTestMethod]
        public void AbsolutePath_Inequality_ShouldWorkCorrectly()
        {
            string testPath = Path.GetTempPath();
            var path1 = new AbsolutePath(testPath);
            var differentPath = new AbsolutePath(Path.Combine(testPath, "different"));

            (path1 != differentPath).ShouldBeTrue();
#pragma warning disable CS1718 // Comparison made to same variable
            (path1 != path1).ShouldBeFalse();
#pragma warning restore CS1718 // Comparison made to same variable
        }

        [MSBuildTestMethod]
        public void AbsolutePath_GetHashCode_ShouldBeConsistentWithEquals()
        {
            string testPath = Path.GetTempPath();
            var path1 = new AbsolutePath(testPath);
            var path2 = new AbsolutePath(testPath);

            // Equal objects must have equal hash codes
            path1.Equals(path2).ShouldBeTrue();
            path1.GetHashCode().ShouldBe(path2.GetHashCode());
        }

        [MSBuildTestMethod]
        public void AbsolutePath_Equals_WithObject_ShouldWorkCorrectly()
        {
            string testPath = Path.GetTempPath();
            var path1 = new AbsolutePath(testPath);
            object path2 = new AbsolutePath(testPath);
            object notAPath = "not a path";

            path1.Equals(path2).ShouldBeTrue();
            path1.Equals(notAPath).ShouldBeFalse();
            path1.Equals(null).ShouldBeFalse();
        }

        [WindowsOnlyFact]
        public void AbsolutePath_CaseInsensitive_OnWindows()
        {
            // On Windows, paths are case-insensitive
            var lowerPath = new AbsolutePath("C:\\foo\\bar", ignoreRootedCheck: true);
            var upperPath = new AbsolutePath("C:\\FOO\\BAR", ignoreRootedCheck: true);

            lowerPath.Equals(upperPath).ShouldBeTrue();
            (lowerPath == upperPath).ShouldBeTrue();
            lowerPath.GetHashCode().ShouldBe(upperPath.GetHashCode());
        }

        [LinuxOnlyFact]
        public void AbsolutePath_CaseSensitive_OnLinux()
        {
            // On Linux, paths are case-sensitive
            var lowerPath = new AbsolutePath("/foo/bar");
            var upperPath = new AbsolutePath("/FOO/BAR");

            lowerPath.Equals(upperPath).ShouldBeFalse();
            (lowerPath == upperPath).ShouldBeFalse();
        }

        [MSBuildTestMethod]
        [DataRow("not/rooted/path", false, true)]
        [DataRow("not/rooted/path", true, false)]
        public void AbsolutePath_RootedValidation_ShouldBehaveProperly(string path, bool ignoreRootedCheck, bool shouldThrow)
        {
            if (shouldThrow)
            {
                Should.Throw<ArgumentException>(() => new AbsolutePath(path, ignoreRootedCheck: ignoreRootedCheck));
            }
            else
            {
                var absolutePath = new AbsolutePath(path, ignoreRootedCheck: ignoreRootedCheck);
                absolutePath.Value.ShouldBe(path);
            }
        }

        [WindowsOnlyTheory]
        // True Windows absolute paths - should be accepted
        [DataRow("C:\\foo", true)]                    // Standard Windows absolute path
        [DataRow("C:\\foo\\bar", true)]                // Another Windows absolute path
        [DataRow("D:\\foo\\bar", true)]                // Different drive Windows path
        [DataRow("C:\\foo\\bar\\.", true)]              // Windows absolute path with current directory
        [DataRow("C:\\foo\\bar\\..", true)]             // Windows absolute path with parent directory
        // Windows rooted but NOT absolute paths - should be rejected
        [DataRow("\\foo", false)]                     // Root-relative (missing drive)
        [DataRow("\\foo\\bar", false)]                 // Root-relative (missing drive)
        [DataRow("C:foo", false)]                    // Drive-relative (no backslash after colon)
        [DataRow("C:1\\foo", false)]                  // Drive-relative with unexpected character
        // Relative paths - should be rejected
        [DataRow("foo", false)]                       // Simple relative path
        [DataRow("foo/bar", false)]                   // Forward slash relative path
        [DataRow("foo\\bar", false)]                  // Backslash relative path
        [DataRow(".", false)]                         // Current directory
        [DataRow("..", false)]                        // Parent directory
        [DataRow("../parent", false)]                 // Parent relative path
        [DataRow("subfolder/file.txt", false)]        // Nested relative path
        public void AbsolutePath_WindowsPathValidation_ShouldAcceptOnlyTrueAbsolutePaths(string path, bool shouldBeAccepted)
        {
            ValidatePathAcceptance(path, shouldBeAccepted);
        }

        [UnixOnlyTheory]
        // True Unix absolute paths - should be accepted
        [DataRow("/foo", true)]                       // Standard Unix absolute path
        [DataRow("/foo/bar", true)]                   // Nested Unix absolute path
        [DataRow("/", true)]                          // Root directory
        [DataRow("/foo/bar/.", true)]                 // Unix absolute path with current directory
        [DataRow("/foo/bar/..", true)]                // Unix absolute path with parent directory
        // Relative paths - should be rejected (same on all platforms)
        [DataRow("foo", false)]                       // Simple relative path
        [DataRow("foo/bar", false)]                   // Forward slash relative path
        [DataRow("foo\\bar", false)]                  // Backslash relative path (unusual on Unix but still relative)
        [DataRow(".", false)]                         // Current directory
        [DataRow("..", false)]                        // Parent directory
        [DataRow("../parent", false)]                 // Parent relative path
        [DataRow("subfolder/file.txt", false)]        // Nested relative path
        public void AbsolutePath_UnixPathValidation_ShouldAcceptOnlyTrueAbsolutePaths(string path, bool shouldBeAccepted)
        {
            ValidatePathAcceptance(path, shouldBeAccepted);
        }

        [MSBuildTestMethod]
        public void GetCanonicalForm_NullPath_ShouldThrow()
        {
            var absolutePath = new AbsolutePath(null!, null!, ignoreRootedCheck: true);

            Should.Throw<ArgumentNullException>(() => absolutePath.GetCanonicalForm());
        }


        [WindowsOnlyTheory]
        [DataRow("C:\\foo\\.\\bar")]                    // Current directory reference
        [DataRow("C:\\foo\\..\\bar")]                   // Parent directory reference
        [DataRow("C:\\foo/bar")]                        // Forward slash to backslash
        [DataRow("C:\\foo\\bar")]                       // Simple Windows path (no normalization needed)
        [DataRow("C:\\foo\\\\bar")]                     // Consecutive backslashes
        [DataRow("C:\\foo\\bar\\\\")]                   // Trailing consecutive backslashes
        public void GetCanonicalForm_WindowsPathNormalization_ShouldMatchPathGetFullPath(string inputPath)
        {
            ValidateGetCanonicalFormMatchesSystem(inputPath);
        }

        [UnixOnlyTheory]
        [DataRow("/foo/./bar")]                         // Current directory reference
        [DataRow("/foo/../bar")]                        // Parent directory reference
        [DataRow("/foo/bar")]                           // Simple Unix path (no normalization needed)
        [DataRow("/foo/bar\\baz")]                      // Simple Unix path with backslash that is not a path separator (no normalization needed)
        [DataRow("/foo//bar")]                          // Consecutive forward slashes
        [DataRow("/foo/bar//")]                         // Trailing consecutive forward slashes
        public void GetCanonicalForm_UnixPathNormalization_ShouldMatchPathGetFullPath(string inputPath)
        {
            ValidateGetCanonicalFormMatchesSystem(inputPath);
        }

        private static void ValidateGetCanonicalFormMatchesSystem(string inputPath)
        {
            var absolutePath = new AbsolutePath(inputPath, ignoreRootedCheck: true);
            var result = absolutePath.GetCanonicalForm();
            var systemResult = Path.GetFullPath(inputPath);

            // Should match Path.GetFullPath behavior exactly
            result.Value.ShouldBe(systemResult);

            // Should preserve original value
            result.OriginalValue.ShouldBe(absolutePath.OriginalValue);
        }

        /// <summary>
        /// Windows rooted-but-not-fully-qualified inputs (root-relative <c>"\foo"</c>, drive-relative <c>"X:foo"</c>)
        /// must be anchored to the supplied base path, not to process state (current drive, per-drive cwd).
        /// Required for multithreaded task isolation. Anchoring happens at construction so both
        /// <see cref="AbsolutePath.Value"/> and the canonical form are deterministic.
        /// </summary>
        [WindowsOnlyTheory]
        // Root-relative — anchored at base path's drive root.
        [DataRow(@"X:\proj", @"\foo", @"X:\foo")]
        [DataRow(@"X:\proj", @"\foo\bar", @"X:\foo\bar")]
        [DataRow(@"X:\proj", @"\sub\dir\file.txt", @"X:\sub\dir\file.txt")]
        // Drive-relative on the same drive as base path — anchored under base path itself.
        [DataRow(@"X:\proj", @"X:foo", @"X:\proj\foo")]
        [DataRow(@"X:\proj", @"X:", @"X:\proj")]
        [DataRow(@"X:\proj", @"X:sub\file.txt", @"X:\proj\sub\file.txt")]
        // Drive-relative with a drive different from base path — drive dropped, remainder anchored.
        [DataRow(@"C:\proj", @"D:foo", @"C:\proj\foo")]
        // Root-relative against UNC base path — re-rooted under the UNC share.
        [DataRow(@"\\server\share\base", @"\foo", @"\\server\share\foo")]
        [DataRow(@"\\server\share\base", @"\sub\file.txt", @"\\server\share\sub\file.txt")]
        // Drive-relative against UNC base path — drive dropped, remainder anchored.
        [DataRow(@"\\server\share\base", @"X:foo", @"\\server\share\base\foo")]
        // Root-relative against DOS device base path (\\?\ and \\.\) — re-rooted under the device root.
        [DataRow(@"\\?\C:\base", @"\foo", @"\\?\C:\foo")]
        [DataRow(@"\\.\C:\base", @"\foo", @"\\.\C:\foo")]
        // Drive-relative against DOS device base path — drive dropped, remainder anchored.
        [DataRow(@"\\?\C:\base", @"X:foo", @"\\?\C:\base\foo")]
        [DataRow(@"\\.\C:\base", @"X:foo", @"\\.\C:\base\foo")]
        // Pass-through: fully qualified inputs win and are not touched by anchoring.
        [DataRow(@"C:\proj", @"D:\absolute\file.txt", @"D:\absolute\file.txt")]
        [DataRow(@"C:\proj", @"\\server\share\file.txt", @"\\server\share\file.txt")]
        [DataRow(@"C:\proj", @"\\?\C:\file.txt", @"\\?\C:\file.txt")]
        // Plain relative input — anchored to basePath via Path.Combine, never touches CWD.
        [DataRow(@"C:\proj", @"foo", @"C:\proj\foo")]
        [DataRow(@"C:\proj", @"sub\file.txt", @"C:\proj\sub\file.txt")]
        public void AnchorsToBasePath_NotProcessState(string baseDir, string input, string expected)
        {
            var basePath = new AbsolutePath(baseDir);
            var combined = new AbsolutePath(input, basePath);

            // Construction anchors rooted-but-not-fully-qualified inputs to basePath, so Value
            // is already fully qualified and does not depend on process state.
            combined.Value.ShouldBe(expected,
                customMessage: $"Construction leaked process state: '{input}' did not anchor to '{baseDir}'.");

            AbsolutePath canonical = combined.GetCanonicalForm();

            canonical.Value.ShouldBe(expected,
                customMessage: $"GetCanonicalForm leaked process state: '{input}' did not anchor to '{baseDir}'.");
            canonical.OriginalValue.ShouldBe(input);
        }

        [MSBuildTestMethod]
        public void GetCanonicalForm_InvalidPathCharacters_ShouldThrowSameAsPathGetFullPath()
        {
            // A path containing a null character is invalid on all platforms.
            // Path.GetFullPath throws for this input; GetCanonicalForm should too.
            // Construct the path string directly to avoid Path.Combine throwing on .NET Framework.
            string invalidPath = Path.GetTempPath() + "foo\0bar";
            var absolutePath = new AbsolutePath(invalidPath, ignoreRootedCheck: true);

            // Capture the exception that Path.GetFullPath would throw
            Exception? getFullPathException = null;
            try
            {
                Path.GetFullPath(invalidPath);
            }
            catch (Exception ex)
            {
                getFullPathException = ex;
            }

            getFullPathException.ShouldNotBeNull("Path.GetFullPath should throw for a path with null character");

            // GetCanonicalForm should throw the same exception type
            Should.Throw(
                () => absolutePath.GetCanonicalForm(),
                getFullPathException.GetType());
        }

        [WindowsOnlyFact]
        [UseInvariantCulture]
        public void AbsolutePath_NotRooted_ShouldThrowWithLocalizedMessage()
        {
            var exception = Should.Throw<ArgumentException>(() => new AbsolutePath("relative/path"));
            exception.Message.ShouldContain("Path must be rooted");
        }
    }
}
