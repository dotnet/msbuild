// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
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
            var packCommand = new PackCommand(testProject, configuration: "Test");
            var result = packCommand.Execute();
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
            var packCommand = new PackCommand(testProject, output: outputDir.FullName);
            var result = packCommand.Execute();
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

            var output = Path.Combine(testInstance.TestRoot, "bin", "Debug", DefaultLibraryFramework, "TestLibraryWithConfiguration.dll");
            var informationalVersion = PeReaderUtils.GetAssemblyAttributeValue(output, "AssemblyInformationalVersionAttribute");

            informationalVersion.Should().NotBeNull();
            informationalVersion.Should().BeEquivalentTo("1.0.0-85");

            var outputPackage = Path.Combine(testInstance.TestRoot, "bin", "Debug", "TestLibraryWithConfiguration.1.0.0-85.nupkg");
            File.Exists(outputPackage).Should().BeTrue(outputPackage);
        }

        [Fact]
        public void HasBuildOutputWhenUsingBuildBasePath()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestLibraryWithConfiguration")
                                                   .WithLockFiles();

            var cmd = new PackCommand(Path.Combine(testInstance.TestRoot, Project.FileName), buildBasePath: "buildBase");
            cmd.Execute().Should().Pass();

            var outputPackage = Path.Combine(testInstance.TestRoot, "bin", "Debug", "TestLibraryWithConfiguration.1.0.0.nupkg");
            File.Exists(outputPackage).Should().BeTrue(outputPackage);

            var zip = ZipFile.Open(outputPackage, ZipArchiveMode.Read);
            zip.Entries.Should().Contain(e => e.FullName == "lib/netstandard1.5/TestLibraryWithConfiguration.dll");
        }

        [Fact]
        public void HasIncludedFiles()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("EndToEndTestApp")
                                                   .WithLockFiles()
                                                   .WithBuildArtifacts();

            var cmd = new PackCommand(Path.Combine(testInstance.TestRoot, Project.FileName));
            cmd.Execute().Should().Pass();

            var outputPackage = Path.Combine(testInstance.TestRoot, "bin", "Debug", "EndToEndTestApp.1.0.0.nupkg");
            File.Exists(outputPackage).Should().BeTrue(outputPackage);

            var zip = ZipFile.Open(outputPackage, ZipArchiveMode.Read);
            zip.Entries.Should().Contain(e => e.FullName == "packfiles/pack1.txt");
            zip.Entries.Should().Contain(e => e.FullName == "newpath/pack2.txt");
        }

        [Fact]
        public void PackAddsCorrectFilesForProjectsWithOutputNameSpecified()
        {
            var testInstance =
                TestAssetsManager
                    .CreateTestInstance("LibraryWithOutputAssemblyName")
                    .WithLockFiles();

            var cmd = new PackCommand(Path.Combine(testInstance.TestRoot, Project.FileName));
            cmd.Execute().Should().Pass();

            var outputPackage = Path.Combine(testInstance.TestRoot, "bin", "Debug", "LibraryWithOutputAssemblyName.1.0.0.nupkg");
            File.Exists(outputPackage).Should().BeTrue(outputPackage);
            var zip = ZipFile.Open(outputPackage, ZipArchiveMode.Read);
            zip.Entries.Should().Contain(e => e.FullName == "lib/netstandard1.5/MyLibrary.dll");

            var symbolsPackage = Path.Combine(testInstance.TestRoot, "bin", "Debug", "LibraryWithOutputAssemblyName.1.0.0.symbols.nupkg");
            File.Exists(symbolsPackage).Should().BeTrue(symbolsPackage);
            zip = ZipFile.Open(symbolsPackage, ZipArchiveMode.Read);
            zip.Entries.Should().Contain(e => e.FullName == "lib/netstandard1.5/MyLibrary.dll");
            zip.Entries.Should().Contain(e => e.FullName == "lib/netstandard1.5/MyLibrary.pdb");
        }

        [Fact]
        public void PackWorksWithLocalProjectJson()
        {
            var testInstance = TestAssetsManager
                .CreateTestInstance("TestAppSimple")
                .WithLockFiles();

            new PackCommand(Project.FileName)
                .WithWorkingDirectory(testInstance.TestRoot)
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void HasServiceableFlagWhenArgumentPassed()
        {
            var root = Temp.CreateDirectory();

            var testLibDir = root.CreateDirectory("TestLibrary");
            var sourceTestLibDir = Path.Combine(_testProjectsRoot, "TestLibraryWithConfiguration");

            CopyProjectToTempDir(sourceTestLibDir, testLibDir);

            var testProject = GetProjectPath(testLibDir);
            var packCommand = new PackCommand(testProject, configuration: "Debug", serviceable: true);
            var result = packCommand.Execute();
            result.Should().Pass();

            var outputDir = new DirectoryInfo(Path.Combine(testLibDir.Path, "bin", "Debug"));
            outputDir.Should().Exist();
            outputDir.Should().HaveFiles(new[] { "TestLibrary.1.0.0.nupkg", "TestLibrary.1.0.0.symbols.nupkg" });

            var outputPackage = Path.Combine(outputDir.FullName, "TestLibrary.1.0.0.nupkg");
            var zip = ZipFile.Open(outputPackage, ZipArchiveMode.Read);
            zip.Entries.Should().Contain(e => e.FullName == "TestLibrary.nuspec");

            var manifestReader = new StreamReader(zip.Entries.First(e => e.FullName == "TestLibrary.nuspec").Open());
            var nuspecXml = XDocument.Parse(manifestReader.ReadToEnd());
            var node = nuspecXml.Descendants().Single(e => e.Name.LocalName == "serviceable");
            Assert.Equal("true", node.Value);
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
