// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    public sealed class TaskFactoryUtilities_Tests
    {
        [Fact]
        public void GetTemporaryTaskAssemblyPath_ShouldReturnValidPath()
        {
            // Act
            string assemblyPath = TaskFactoryUtilities.GetTemporaryTaskAssemblyPath();

            // Assert
            assemblyPath.ShouldNotBeNull();
            assemblyPath.ShouldEndWith(".dll");
        }

        [Fact]
        public void CreateLoadManifest_ShouldCreateFileWithDirectories()
        {
            using (var env = TestEnvironment.Create())
            {
                // Arrange
                var tempAssemblyFile = env.CreateFile(".dll");
                var directories = new List<string> { "dir1", "dir2" };

                // Act
                string manifestPath = TaskFactoryUtilities.CreateLoadManifest(tempAssemblyFile.Path, directories);

                // Assert
                manifestPath.ShouldBe(tempAssemblyFile.Path + TaskFactoryUtilities.InlineTaskLoadManifestSuffix);
                File.Exists(manifestPath).ShouldBeTrue();

                string[] manifestContent = File.ReadAllLines(manifestPath);
                manifestContent.Length.ShouldBe(2);
                manifestContent.ShouldContain("dir1");
                manifestContent.ShouldContain("dir2");
            }
        }

        [Fact]
        public void CreateAssemblyResolver_ShouldReturnValidHandler()
        {
            // Arrange
            var directories = new List<string> { Environment.CurrentDirectory };

            // Act
            ResolveEventHandler handler = TaskFactoryUtilities.CreateAssemblyResolver(directories);

            // Assert
            handler.ShouldNotBeNull();
        }
    }
}
