// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Microsoft.Build.Framework.PathHelpers;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class AbsolutePath_Tests
    {
        [Fact]
        public void AbsolutePath_FromAbsolutePath_ShouldPreservePath()
        {
            // Arrange
            string absolutePathString = Path.GetTempPath();

            // Act
            var absolutePath = new AbsolutePath(absolutePathString);

            // Assert
            absolutePath.Path.ShouldBe(absolutePathString);
            Path.IsPathRooted(absolutePath.Path).ShouldBeTrue();
        }

        [Theory]
        [InlineData("subfolder", "should resolve relative path against base")]
        [InlineData("deep/nested/path", "should handle nested relative paths")]
        [InlineData(".", "should resolve to base directory")]
        [InlineData("", "empty path should resolve to base directory")]
        [InlineData("..", "should resolve to parent directory")]
        public void AbsolutePath_FromRelativePath_ShouldResolveAgainstBase(string relativePath, string description)
        {
            // Arrange
            string baseDirectory = Path.Combine(Path.GetTempPath(), "testfolder");
            var basePath = new AbsolutePath(baseDirectory);

            // Act
            var absolutePath = new AbsolutePath(relativePath, basePath);

            // Assert - {description}
            Path.IsPathRooted(absolutePath.Path).ShouldBeTrue();
            
            string expectedPath = Path.Combine(baseDirectory, relativePath);
            absolutePath.Path.ShouldBe(expectedPath);
        }

        [Fact]
        public void AbsolutePath_Equality_ShouldWorkCorrectly()
        {
            // Arrange
            string testPath = Path.GetTempPath();
            var path1 = new AbsolutePath(testPath);
            var path2 = new AbsolutePath(testPath);
            var differentPath = new AbsolutePath(Path.Combine(testPath, "different"));

            // Act & Assert
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
            // Act & Assert
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
    }
}
