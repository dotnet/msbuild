// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class AbsolutePath_Tests
    {
        private static void ValidatePathAcceptance(string path, bool shouldBeAccepted)
        {
            if (shouldBeAccepted)
            {
                // Should not throw - these are truly absolute paths
                var absolutePath = new AbsolutePath(path);
                absolutePath.Path.ShouldBe(path);
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

            absolutePath.Path.ShouldBe(absolutePathString);
            Path.IsPathRooted(absolutePath.Path).ShouldBeTrue();
        }

        [Theory]
        [InlineData("subfolder")]
        [InlineData("deep/nested/path")]
        [InlineData(".")]
        [InlineData("")]
        [InlineData("..")]
        public void AbsolutePath_FromRelativePath_ShouldResolveAgainstBase(string relativePath)
        {
            string baseDirectory = Path.Combine(Path.GetTempPath(), "testfolder");
            var basePath = new AbsolutePath(baseDirectory);
            var absolutePath = new AbsolutePath(relativePath, basePath);

            Path.IsPathRooted(absolutePath.Path).ShouldBeTrue();
            
            string expectedPath = Path.Combine(baseDirectory, relativePath);
            absolutePath.Path.ShouldBe(expectedPath);
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
                absolutePath.Path.ShouldBe(path);
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

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void AbsolutePath_NullOrEmptyPath_ShouldBeRejected(string path)
        {
            Should.Throw<System.ArgumentException>(() => new AbsolutePath(path, ignoreRootedCheck: false));
        }
    }
}
