// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Compiler.Tests
{
    public class PackTests : TestBase
    {
        private readonly string _testProjectsRoot;

        public PackTests()
        {
            _testProjectsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects");
        }

        [Fact]
        public void OutputsPackagesToConfigurationSubdirWhenOutputParameterIsNotPassed()
        {
            var root = Temp.CreateDirectory();

            var testLibDir = root.CreateDirectory("TestLibrary");
            var sourceTestLibDir = Path.Combine(_testProjectsRoot, "TestLibraryWithConfiguration");

            CopyProjectToTempDir(sourceTestLibDir, testLibDir);

            var testProject = GetProjectPath(testLibDir);
            var buildCommand = new PackCommand(testProject, configuration: "Test");
            var result = buildCommand.Execute();
            result.Should().Pass();

            var outputDir = new DirectoryInfo(Path.Combine(testLibDir.Path, "bin", "Test"));
            outputDir.Should().Exist();
            outputDir.Should().HaveFiles(new [] { "TestLibrary.1.0.0.nupkg" , "TestLibrary.1.0.0.symbols.nupkg" });
        }

        [Fact]
        public void OutputsPackagesFlatIntoOutputDirWhenOutputParameterIsPassed()
        {
            var root = Temp.CreateDirectory();

            var testLibDir = root.CreateDirectory("TestLibrary");
            var sourceTestLibDir = Path.Combine(_testProjectsRoot, "TestLibraryWithConfiguration");

            CopyProjectToTempDir(sourceTestLibDir, testLibDir);

            var outputDir = new DirectoryInfo(Path.Combine(testLibDir.Path, "bin2"));
            var testProject = GetProjectPath(testLibDir);
            var buildCommand = new PackCommand(testProject, output: outputDir.FullName);
            var result = buildCommand.Execute();
            result.Should().Pass();

            outputDir.Should().Exist();
            outputDir.Should().HaveFiles(new[] { "TestLibrary.1.0.0.nupkg", "TestLibrary.1.0.0.symbols.nupkg" });
        }
        
        [Fact]
        public void SettingVersionSuffixFlag_ShouldStampAssemblyInfoInOutputAssemblyAndPackage()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestLibraryWithConfiguration")
                                                .WithLockFiles();

            var cmd = new PackCommand(Path.Combine(testInstance.TestRoot, Project.FileName),  versionSuffix: "85");
            cmd.Execute().Should().Pass();

            var output = Path.Combine(testInstance.TestRoot, "bin", "Debug", DefaultFramework, "TestLibraryWithConfiguration.dll");
            var informationalVersion = PeReaderUtils.GetAssemblyAttributeValue(output, "AssemblyInformationalVersionAttribute");
            
            informationalVersion.Should().NotBeNull();
            informationalVersion.Should().BeEquivalentTo("1.0.0-85");
            
            var outputPackage = Path.Combine(testInstance.TestRoot, "bin", "Debug", "TestLibraryWithConfiguration.1.0.0-85.nupkg");
            File.Exists(outputPackage).Should().BeTrue(outputPackage);
        }

        private void CopyProjectToTempDir(string projectDir, TempDirectory tempDir)
        {
            // copy all the files to temp dir
            foreach (var file in Directory.EnumerateFiles(projectDir))
            {
                tempDir.CopyFile(file);
            }
        }

        private string GetProjectPath(TempDirectory projectDir)
        {
            return Path.Combine(projectDir.Path, "project.json");
        }
    }
}
