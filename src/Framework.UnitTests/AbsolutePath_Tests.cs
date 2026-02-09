// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;
using Xunit.NetCore.Extensions;

namespace Microsoft.Build.UnitTests
{
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
                Should.Throw<System.ArgumentException>(() => new AbsolutePath(path, ignoreRootedCheck: false),
                    $"Path '{path}' should be rejected as it's not a true absolute path");
            }
        }

        [Fact]
        public void AbsolutePath_FromAbsolutePath_ShouldPreservePath()
        {
            string absolutePathString = Path.GetTempPath();
            var absolutePath = new AbsolutePath(absolutePathString);

            absolutePath.Value.ShouldBe(absolutePathString);
            Path.IsPathRooted(absolutePath.Value).ShouldBeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [UseInvariantCulture]
        public void AbsolutePath_NullOrEmpty_ShouldThrow(string? path)
        {
            var exception = Should.Throw<ArgumentException>(() => new AbsolutePath(path!));
            exception.Message.ShouldContain("Path must not be null or empty");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [UseInvariantCulture]
        public void AbsolutePath_NullOrEmptyWithBasePath_ShouldThrow(string? path)
        {
            var basePath = GetTestBasePath();
            var exception = Should.Throw<ArgumentException>(() => new AbsolutePath(path!, basePath));
            exception.Message.ShouldContain("Path must not be null or empty");
        }

        [Theory]
        [InlineData("subfolder")]
        [InlineData("deep/nested/path")]
        [InlineData(".")]
        [InlineData("..")]
        public void AbsolutePath_FromRelativePath_ShouldResolveAgainstBase(string relativePath)
        {
            string baseDirectory = Path.Combine(Path.GetTempPath(), "testfolder");
            var basePath = new AbsolutePath(baseDirectory);
            var absolutePath = new AbsolutePath(relativePath, basePath);

            Path.IsPathRooted(absolutePath.Value).ShouldBeTrue();
            
            string expectedPath = Path.Combine(baseDirectory, relativePath);
            absolutePath.Value.ShouldBe(expectedPath);
        }

        [Fact]
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

        [Fact]
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

        [Fact]
        public void AbsolutePath_GetHashCode_ShouldBeConsistentWithEquals()
        {
            string testPath = Path.GetTempPath();
            var path1 = new AbsolutePath(testPath);
            var path2 = new AbsolutePath(testPath);

            // Equal objects must have equal hash codes
            path1.Equals(path2).ShouldBeTrue();
            path1.GetHashCode().ShouldBe(path2.GetHashCode());
        }

        [Fact]
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

        [Theory]
        [InlineData("not/rooted/path", false, true)]
        [InlineData("not/rooted/path", true, false)]
        public void AbsolutePath_RootedValidation_ShouldBehaveProperly(string path, bool ignoreRootedCheck, bool shouldThrow)
        {
            if (shouldThrow)
            {
                Should.Throw<System.ArgumentException>(() => new AbsolutePath(path, ignoreRootedCheck: ignoreRootedCheck));
            }
            else
            {
                var absolutePath = new AbsolutePath(path, ignoreRootedCheck: ignoreRootedCheck);
                absolutePath.Value.ShouldBe(path);
            }
        }

        [WindowsOnlyTheory]
        // True Windows absolute paths - should be accepted
        [InlineData("C:\\foo", true)]                    // Standard Windows absolute path
        [InlineData("C:\\foo\\bar", true)]                // Another Windows absolute path
        [InlineData("D:\\foo\\bar", true)]                // Different drive Windows path
        [InlineData("C:\\foo\\bar\\.", true)]              // Windows absolute path with current directory
        [InlineData("C:\\foo\\bar\\..", true)]             // Windows absolute path with parent directory
        // Windows rooted but NOT absolute paths - should be rejected
        [InlineData("\\foo", false)]                     // Root-relative (missing drive)
        [InlineData("\\foo\\bar", false)]                 // Root-relative (missing drive)
        [InlineData("C:foo", false)]                    // Drive-relative (no backslash after colon)
        [InlineData("C:1\\foo", false)]                  // Drive-relative with unexpected character
        // Relative paths - should be rejected
        [InlineData("foo", false)]                       // Simple relative path
        [InlineData("foo/bar", false)]                   // Forward slash relative path
        [InlineData("foo\\bar", false)]                  // Backslash relative path
        [InlineData(".", false)]                         // Current directory
        [InlineData("..", false)]                        // Parent directory
        [InlineData("../parent", false)]                 // Parent relative path
        [InlineData("subfolder/file.txt", false)]        // Nested relative path
        public void AbsolutePath_WindowsPathValidation_ShouldAcceptOnlyTrueAbsolutePaths(string path, bool shouldBeAccepted)
        {
            ValidatePathAcceptance(path, shouldBeAccepted);
        }

        [UnixOnlyTheory]
        // True Unix absolute paths - should be accepted
        [InlineData("/foo", true)]                       // Standard Unix absolute path
        [InlineData("/foo/bar", true)]                   // Nested Unix absolute path
        [InlineData("/", true)]                          // Root directory
        [InlineData("/foo/bar/.", true)]                 // Unix absolute path with current directory
        [InlineData("/foo/bar/..", true)]                // Unix absolute path with parent directory
        // Relative paths - should be rejected (same on all platforms)
        [InlineData("foo", false)]                       // Simple relative path
        [InlineData("foo/bar", false)]                   // Forward slash relative path
        [InlineData("foo\\bar", false)]                  // Backslash relative path (unusual on Unix but still relative)
        [InlineData(".", false)]                         // Current directory
        [InlineData("..", false)]                        // Parent directory
        [InlineData("../parent", false)]                 // Parent relative path
        [InlineData("subfolder/file.txt", false)]        // Nested relative path
        public void AbsolutePath_UnixPathValidation_ShouldAcceptOnlyTrueAbsolutePaths(string path, bool shouldBeAccepted)
        {
            ValidatePathAcceptance(path, shouldBeAccepted);
        }

        [Fact]
        public void GetCanonicalForm_NullPath_ShouldReturnSameInstance()
        {
            var absolutePath = new AbsolutePath(null!, null!, ignoreRootedCheck: true);
            var result = absolutePath.GetCanonicalForm();
            
            // Should return the same struct values when no normalization is needed
            result.ShouldBe(absolutePath);
        }


        [WindowsOnlyTheory]
        [InlineData("C:\\foo\\.\\bar")]                    // Current directory reference
        [InlineData("C:\\foo\\..\\bar")]                   // Parent directory reference
        [InlineData("C:\\foo/bar")]                        // Forward slash to backslash
        [InlineData("C:\\foo\\bar")]                       // Simple Windows path (no normalization needed)
        public void GetCanonicalForm_WindowsPathNormalization_ShouldMatchPathGetFullPath(string inputPath)
        {
            ValidateGetCanonicalFormMatchesSystem(inputPath);
        }

        [UnixOnlyTheory]
        [InlineData("/foo/./bar")]                         // Current directory reference
        [InlineData("/foo/../bar")]                        // Parent directory reference     
        [InlineData("/foo/bar")]                           // Simple Unix path (no normalization needed)
        [InlineData("/foo/bar\\baz")]                      // Simple Unix path with backslash that is not a path separator (no normalization needed)
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
      
        [WindowsOnlyFact]
        [UseInvariantCulture]
        public void AbsolutePath_NotRooted_ShouldThrowWithLocalizedMessage()
        {
            var exception = Should.Throw<ArgumentException>(() => new AbsolutePath("relative/path"));
            exception.Message.ShouldContain("Path must be rooted");
        }
    }
}
