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
        public void CreateProcessSpecificTaskDirectory_ShouldCreateValidDirectory()
        {
            // Act
            string directory = TaskFactoryUtilities.CreateProcessSpecificTaskDirectory();

            // Assert
            directory.ShouldNotBeNull();
            Directory.Exists(directory).ShouldBeTrue();
            directory.ShouldContain(MSBuildConstants.InlineTaskTempDllSubPath);
            directory.ShouldContain($"pid_{EnvironmentUtilities.CurrentProcessId}");
        }

        [Fact]
        public void GetTemporaryTaskAssemblyPath_ShouldReturnValidPath()
        {
            // Act
            string assemblyPath = TaskFactoryUtilities.GetTemporaryTaskAssemblyPath();

            // Assert
            assemblyPath.ShouldNotBeNull();
            assemblyPath.ShouldEndWith(".dll");
            Path.GetDirectoryName(assemblyPath).ShouldContain(MSBuildConstants.InlineTaskTempDllSubPath);
        }

        [Fact]
        public void GetTemporaryTaskAssemblyPath_WithCustomParameters_ShouldUseCustomValues()
        {
            // Arrange
            string fileName = "custom_task";
            string extension = ".exe";

            // Act
            string assemblyPath = TaskFactoryUtilities.GetTemporaryTaskAssemblyPath(fileName, extension);

            // Assert
            assemblyPath.ShouldNotBeNull();
            assemblyPath.ShouldEndWith(extension);
            Path.GetFileName(assemblyPath).ShouldContain(fileName);
        }

        [Fact]
        public void CreateLoadManifest_ShouldCreateFileWithDirectories()
        {
            // Arrange
            string tempAssemblyPath = Path.GetTempFileName();
            var directories = new List<string> { "dir1", "dir2", "dir3" };

            try
            {
                // Act
                string manifestPath = TaskFactoryUtilities.CreateLoadManifest(tempAssemblyPath, directories);

                // Assert
                manifestPath.ShouldBe(tempAssemblyPath + ".loadmanifest");
                File.Exists(manifestPath).ShouldBeTrue();

                string[] manifestContent = File.ReadAllLines(manifestPath);
                manifestContent.Length.ShouldBe(3);
                manifestContent.ShouldContain("dir1");
                manifestContent.ShouldContain("dir2");
                manifestContent.ShouldContain("dir3");
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempAssemblyPath))
                {
                    File.Delete(tempAssemblyPath);
                }
                if (File.Exists(tempAssemblyPath + ".loadmanifest"))
                {
                    File.Delete(tempAssemblyPath + ".loadmanifest");
                }
            }
        }

        [Fact]
        public void CreateLoadManifest_WithNullAssemblyPath_ShouldThrow()
        {
            // Arrange
            var directories = new List<string> { "dir1" };

            // Act & Assert
            Should.Throw<ArgumentException>(() => TaskFactoryUtilities.CreateLoadManifest(null, directories));
        }

        [Fact]
        public void CreateLoadManifest_WithNullDirectories_ShouldThrow()
        {
            // Arrange
            string assemblyPath = "test.dll";

            // Act & Assert
            Should.Throw<ArgumentNullException>(() => TaskFactoryUtilities.CreateLoadManifest(assemblyPath, null));
        }

        [Fact]
        public void LoadTaskAssembly_WithNullPath_ShouldThrow()
        {
            // Act & Assert
            Should.Throw<ArgumentException>(() => TaskFactoryUtilities.LoadTaskAssembly(null, false));
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

        [Fact]
        public void CreateAssemblyResolver_WithNullDirectories_ShouldThrow()
        {
            Should.Throw<ArgumentNullException>(() => TaskFactoryUtilities.CreateAssemblyResolver(null));
        }
    }
}
