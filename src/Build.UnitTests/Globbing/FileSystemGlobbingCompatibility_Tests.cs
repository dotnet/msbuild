// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Globbing;
using Microsoft.Build.Shared;
using Microsoft.Build.Framework;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.Globbing
{
    /// <summary>
    /// Tests to verify compatibility between MSBuildGlob and FileSystemGlobbingMSBuildGlob implementations
    /// </summary>
    public class FileSystemGlobbingCompatibility_Tests
    {
        public static IEnumerable<object[]> GetTestCases()
        {
            yield return new object[] { "", "*.cs", "test.cs", true };
            yield return new object[] { "", "*.cs", "test.txt", false };
            yield return new object[] { "", "**/*.cs", "folder/test.cs", true };
            yield return new object[] { "", "**/*.cs", "folder/subfolder/test.cs", true };
            yield return new object[] { "", "src/**/*.cs", "src/folder/test.cs", true };
            yield return new object[] { "", "src/**/*.cs", "other/folder/test.cs", false };
            yield return new object[] { "", "folder/*.cs", "folder/test.cs", true };
            yield return new object[] { "", "folder/*.cs", "folder/subfolder/test.cs", false };
            
            var globRoot = NativeMethodsShared.IsWindows ? @"c:\test" : "/test";
            yield return new object[] { globRoot, "*.cs", "test.cs", true };
            yield return new object[] { globRoot, "*.cs", "test.txt", false };
            yield return new object[] { globRoot, "**/*.cs", "folder/test.cs", true };
        }

        [Theory]
        [MemberData(nameof(GetTestCases))]
        public void BothImplementationsShouldMatch(string globRoot, string fileSpec, string testPath, bool expectedMatch)
        {
            // Test original implementation
            var originalGlob = MSBuildGlob.Parse(globRoot, fileSpec);
            var originalResult = originalGlob.IsMatch(testPath);

            // Test FileSystemGlobbing implementation directly
            var fileSystemGlob = FileSystemGlobbingMSBuildGlob.Parse(globRoot, fileSpec);
            var fileSystemResult = fileSystemGlob.IsMatch(testPath);

            // Both should return the same result
            Assert.Equal(expectedMatch, originalResult);
            Assert.Equal(expectedMatch, fileSystemResult);
            Assert.Equal(originalResult, fileSystemResult);
        }

        [Fact]
        public void MSBuildGlobWithTraitEnabledShouldUseFileSystemGlobbing()
        {
            var originalTraitValue = GetTraitValue();
            
            try
            {
                // Enable the trait
                SetTraitValue(true);

                var glob = MSBuildGlob.Parse("", "*.cs");
                var result = glob.IsMatch("test.cs");

                // Should return true for this simple case
                Assert.True(result);
            }
            finally
            {
                // Restore the original trait value
                SetTraitValue(originalTraitValue);
            }
        }

        [Fact]
        public void MSBuildGlobWithTraitDisabledShouldUseOriginalImplementation()
        {
            var originalTraitValue = GetTraitValue();
            
            try
            {
                // Disable the trait
                SetTraitValue(false);

                var glob = MSBuildGlob.Parse("", "*.cs");
                var result = glob.IsMatch("test.cs");

                // Should return true for this simple case using original implementation
                Assert.True(result);
            }
            finally
            {
                // Restore the original trait value
                SetTraitValue(originalTraitValue);
            }
        }

        [Theory]
        [MemberData(nameof(GetTestCases))]
        public void MSBuildGlobBothTraitValuesShouldMatch(string globRoot, string fileSpec, string testPath, bool expectedMatch)
        {
            var originalTraitValue = GetTraitValue();
            
            try
            {
                // Test with trait disabled (original implementation)
                SetTraitValue(false);
                var glob1 = MSBuildGlob.Parse(globRoot, fileSpec);
                var result1 = glob1.IsMatch(testPath);

                // Test with trait enabled (FileSystemGlobbing implementation)
                SetTraitValue(true);
                var glob2 = MSBuildGlob.Parse(globRoot, fileSpec);
                var result2 = glob2.IsMatch(testPath);

                // Both should return the same result
                Assert.Equal(expectedMatch, result1);
                Assert.Equal(expectedMatch, result2);
                Assert.Equal(result1, result2);
            }
            finally
            {
                // Restore the original trait value
                SetTraitValue(originalTraitValue);
            }
        }

        private bool GetTraitValue()
        {
            return Traits.Instance.UseFileSystemGlobbingForMSBuildGlob;
        }

        private void SetTraitValue(bool value)
        {
            Environment.SetEnvironmentVariable("MSBUILD_USE_FILESYSTEMGLOBBING", value ? "1" : null);
            // Force re-creation of Traits instance to pick up the new environment variable
            Traits.UpdateFromEnvironment();
        }
    }
}