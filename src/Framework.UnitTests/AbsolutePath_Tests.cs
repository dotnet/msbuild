// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

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

        #region CreateFromRelative Tests

        [Theory]
        [InlineData("subfolder")]
        [InlineData("deep/nested/path")]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("./relative")]
        [InlineData("../parent")]
        [InlineData("file.txt")]
        public void CreateFromRelative_WithValidRelativePath_ReturnsAbsolutePath(string relativePath)
        {
            // Arrange
            AbsolutePath basePath = GetTestBasePath();
            string expectedPath = Path.Combine(basePath.Value, relativePath);

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(relativePath, basePath);

            // Assert
            result.Value.ShouldBe(expectedPath);
            result.OriginalValue.ShouldBe(relativePath);
            Path.IsPathRooted(result.Value).ShouldBeTrue();
        }

        [Fact]
        public void CreateFromRelative_WithEmptyPath_WhenWave18_4Enabled_ThrowsArgumentException()
        {
            // Arrange - Wave18_4 is enabled by default
            using TestEnvironment testEnv = TestEnvironment.Create();
            ChangeWaves.ResetStateForTests();
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            AbsolutePath basePath = GetTestBasePath();

            // Act & Assert
            var exception = Should.Throw<ArgumentException>(() => AbsolutePath.CreateFromRelative(string.Empty, basePath));
            exception.ParamName.ShouldBe("path");
            exception.Message.ShouldContain("must not be empty");

            ChangeWaves.ResetStateForTests();
        }

        [Fact]
        public void CreateFromRelative_WithNullPath_WhenWave18_4Enabled_ThrowsArgumentNullException()
        {
            // Arrange - Wave18_4 is enabled by default
            using TestEnvironment testEnv = TestEnvironment.Create();
            ChangeWaves.ResetStateForTests();
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            AbsolutePath basePath = GetTestBasePath();

            // Act & Assert - Path.Combine throws ArgumentNullException for null path
            Should.Throw<ArgumentNullException>(() => AbsolutePath.CreateFromRelative(null!, basePath));

            ChangeWaves.ResetStateForTests();
        }

        [Fact]
        public void CreateFromRelative_WithEmptyPath_WhenWave18_4Disabled_ReturnsEmptyPath()
        {
            // Arrange
            using TestEnvironment testEnv = TestEnvironment.Create();
            ChangeWaves.ResetStateForTests();
            testEnv.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_4.ToString());
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            AbsolutePath basePath = GetTestBasePath();

            // Act - Legacy behavior returns empty path as-is
            AbsolutePath result = AbsolutePath.CreateFromRelative(string.Empty, basePath);

            // Assert
            result.Value.ShouldBe(string.Empty);
            result.OriginalValue.ShouldBe(string.Empty);

            ChangeWaves.ResetStateForTests();
        }

        [Fact]
        public void CreateFromRelative_WithNullPath_WhenWave18_4Disabled_ReturnsNullPath()
        {
            // Arrange
            using TestEnvironment testEnv = TestEnvironment.Create();
            ChangeWaves.ResetStateForTests();
            testEnv.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_4.ToString());
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            AbsolutePath basePath = GetTestBasePath();

            // Act - Legacy behavior returns null path as-is
            AbsolutePath result = AbsolutePath.CreateFromRelative(null!, basePath);

            // Assert
            result.Value.ShouldBeNull();
            result.OriginalValue.ShouldBeNull();

            ChangeWaves.ResetStateForTests();
        }

        [WindowsOnlyTheory]
        [InlineData("C:\\absolute\\path")]
        [InlineData("D:\\another\\absolute")]
        public void CreateFromRelative_WithWindowsAbsolutePath_ReturnsAbsolutePath(string absolutePath)
        {
            // Arrange
            AbsolutePath basePath = GetTestBasePath();
            // Path.Combine with an absolute second path returns the second path
            string expectedPath = absolutePath;

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(absolutePath, basePath);

            // Assert
            result.Value.ShouldBe(expectedPath);
            result.OriginalValue.ShouldBe(absolutePath);
        }

        [UnixOnlyTheory]
        [InlineData("/absolute/path")]
        [InlineData("/another/absolute")]
        public void CreateFromRelative_WithUnixAbsolutePath_ReturnsAbsolutePath(string absolutePath)
        {
            // Arrange
            AbsolutePath basePath = GetTestBasePath();
            // Path.Combine with an absolute second path returns the second path
            string expectedPath = absolutePath;

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(absolutePath, basePath);

            // Assert
            result.Value.ShouldBe(expectedPath);
            result.OriginalValue.ShouldBe(absolutePath);
        }

        [Fact]
        public void CreateFromRelative_WithNestedRelativePath_CombinesCorrectly()
        {
            // Arrange
            AbsolutePath basePath = GetTestBasePath();
            string relativePath = Path.Combine("level1", "level2", "level3", "file.txt");
            string expectedPath = Path.Combine(basePath.Value, relativePath);

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(relativePath, basePath);

            // Assert
            result.Value.ShouldBe(expectedPath);
            result.OriginalValue.ShouldBe(relativePath);
        }

        [Fact]
        public void CreateFromRelative_WithCurrentDirectory_CombinesCorrectly()
        {
            // Arrange
            AbsolutePath basePath = GetTestBasePath();
            string relativePath = ".";
            string expectedPath = Path.Combine(basePath.Value, relativePath);

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(relativePath, basePath);

            // Assert
            result.Value.ShouldBe(expectedPath);
            result.OriginalValue.ShouldBe(relativePath);
        }

        [Fact]
        public void CreateFromRelative_WithParentDirectory_CombinesCorrectly()
        {
            // Arrange
            AbsolutePath basePath = GetTestBasePath();
            string relativePath = "..";
            string expectedPath = Path.Combine(basePath.Value, relativePath);

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(relativePath, basePath);

            // Assert
            result.Value.ShouldBe(expectedPath);
            result.OriginalValue.ShouldBe(relativePath);
        }

        [Theory]
        [InlineData("../sibling")]
        [InlineData("../../grandparent")]
        [InlineData("../sibling/child")]
        public void CreateFromRelative_WithParentTraversal_CombinesCorrectly(string relativePath)
        {
            // Arrange
            AbsolutePath basePath = GetTestBasePath();
            string expectedPath = Path.Combine(basePath.Value, relativePath);

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(relativePath, basePath);

            // Assert
            result.Value.ShouldBe(expectedPath);
            result.OriginalValue.ShouldBe(relativePath);
        }

        [Fact]
        public void CreateFromRelative_WithPathContainingSpaces_CombinesCorrectly()
        {
            // Arrange
            AbsolutePath basePath = GetTestBasePath();
            string relativePath = "folder with spaces/file name.txt";
            string expectedPath = Path.Combine(basePath.Value, relativePath);

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(relativePath, basePath);

            // Assert
            result.Value.ShouldBe(expectedPath);
            result.OriginalValue.ShouldBe(relativePath);
        }

        [Fact]
        public void CreateFromRelative_PreservesOriginalValue()
        {
            // Arrange
            AbsolutePath basePath = GetTestBasePath();
            string relativePath = "some/relative/path";

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(relativePath, basePath);

            // Assert - OriginalValue should be the relative path, not the combined path
            result.OriginalValue.ShouldBe(relativePath);
            result.Value.ShouldNotBe(relativePath);
            result.Value.ShouldContain(relativePath);
        }

        [Fact]
        public void CreateFromRelative_ResultIsRooted()
        {
            // Arrange
            AbsolutePath basePath = GetTestBasePath();
            string relativePath = "any/relative/path";

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(relativePath, basePath);

            // Assert
            Path.IsPathRooted(result.Value).ShouldBeTrue();
        }

        [Fact]
        public void CreateFromRelative_WithWhitespaceOnlyPath_WhenWave18_4Enabled_CombinesCorrectly()
        {
            // Whitespace-only is not empty, so it should be processed by Path.Combine
            using TestEnvironment testEnv = TestEnvironment.Create();
            ChangeWaves.ResetStateForTests();
            BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

            AbsolutePath basePath = GetTestBasePath();
            string relativePath = "   ";
            string expectedPath = Path.Combine(basePath.Value, relativePath);

            // Act
            AbsolutePath result = AbsolutePath.CreateFromRelative(relativePath, basePath);

            // Assert
            result.Value.ShouldBe(expectedPath);
            result.OriginalValue.ShouldBe(relativePath);

            ChangeWaves.ResetStateForTests();
        }

        [Fact]
        public void CreateFromRelative_ChangeWaveTransition_BehaviorChangesCorrectly()
        {
            // This test verifies that the behavior changes when the wave is enabled/disabled
            AbsolutePath basePath = GetTestBasePath();

            // Test 1: Wave disabled - empty path returns as-is
            using (TestEnvironment testEnv1 = TestEnvironment.Create())
            {
                ChangeWaves.ResetStateForTests();
                testEnv1.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", ChangeWaves.Wave18_4.ToString());
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                AbsolutePath result = AbsolutePath.CreateFromRelative(string.Empty, basePath);
                result.Value.ShouldBe(string.Empty);

                ChangeWaves.ResetStateForTests();
            }

            // Test 2: Wave enabled - empty path throws
            using (TestEnvironment testEnv2 = TestEnvironment.Create())
            {
                ChangeWaves.ResetStateForTests();
                BuildEnvironmentHelper.ResetInstance_ForUnitTestsOnly();

                Should.Throw<ArgumentException>(() => AbsolutePath.CreateFromRelative(string.Empty, basePath));

                ChangeWaves.ResetStateForTests();
            }
        }

        #endregion
    }
}
