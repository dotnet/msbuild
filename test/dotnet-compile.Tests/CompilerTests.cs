// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Tools.Compiler.Tests
{
    public class CompilerTests : TestBase
    {
        [Fact]
        public void XmlDocumentationFileIsGenerated()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary");

            var testLibDir = Path.Combine(testInstance.TestRoot, "TestLibrary");

            // run compile
            var outputDir = Path.Combine(testLibDir, "bin");
            var testProject = Path.Combine(testLibDir, "project.json");
            var buildCommand = new BuildCommand(testProject, output: outputDir, framework: DefaultLibraryFramework);
            var result = buildCommand.ExecuteWithCapturedOutput();
            result.Should().Pass();

            // verify the output xml file
            var outputXml = Path.Combine(outputDir, "Debug", DefaultLibraryFramework, "TestLibrary.xml");
            Console.WriteLine("OUTPUT XML PATH: " + outputXml);
            Assert.True(File.Exists(outputXml));
            Assert.Contains("Gets the message from the helper", File.ReadAllText(outputXml));
        }

        [Fact]
        public void SatelliteAssemblyIsGeneratedByDotnetBuild()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestProjectWithCultureSpecificResource");

            var testLibDir = testInstance.TestRoot;

            // run compile on a project with resources
            var outputDir = Path.Combine(testLibDir, "bin");
            var testProject = Path.Combine(testLibDir, "project.json");
            var buildCmd = new BuildCommand(testProject, output: outputDir, framework: DefaultFramework);
            var result = buildCmd.ExecuteWithCapturedOutput();
            result.Should().Pass();

            var generatedSatelliteAssemblyPath = Path.Combine(
                outputDir,
                "Debug",
                DefaultFramework,
                "fr",
                "TestProjectWithCultureSpecificResource.resources.dll");
            Assert.True(File.Exists(generatedSatelliteAssemblyPath), $"File {generatedSatelliteAssemblyPath} was not found.");
        }

        [Fact]
        public void LibraryWithAnalyzer()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestLibraryWithAnalyzer");

            var testLibDir = testInstance.TestRoot;

            // run compile
            var outputDir = Path.Combine(testLibDir, "bin");
            var testProject = Path.Combine(testLibDir, "project.json");
            var buildCmd = new BuildCommand(testProject, output: outputDir, framework: DefaultLibraryFramework);
            var result = buildCmd.ExecuteWithCapturedOutput();
            result.Should().Pass();

            Assert.Contains("CA1018", result.StdErr);
        }

        [Fact]
        public void CompilingAppWithPreserveCompilationContextWithSpaceInThePathShouldSucceed()
        {
            var root = Temp.CreateDirectory();

            var spaceBufferDirectory = root.CreateDirectory("space directory");

            var testInstance = TestAssetsManager.CreateTestInstance("TestAppCompilationContext");

            var testAppDir = testInstance.TestRoot;

            Directory.Move(testInstance.TestRoot, spaceBufferDirectory.Path);

            Directory.Move(spaceBufferDirectory.Path, testInstance.TestRoot);

            var testProjectDir = Path.Combine(
                    Directory.GetParent(testInstance.TestRoot).FullName, 
                    "space directory", 
                    "TestAppCompilationContext", 
                    "TestApp");

            var testProject = Path.Combine(testProjectDir, "project.json");

            var buildCommand = new BuildCommand(testProject);

            buildCommand.Execute().Should().Pass();
        }

        [Fact]
        public void ContentFilesAreCopied()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithContentPackage")
                                                .WithLockFiles();

            var root = testInstance.TestRoot;

            // run compile
            var outputDir = Path.Combine(root, "bin");
            var testProject = ProjectUtils.GetProjectJson(root, "TestAppWithContentPackage");
            var buildCommand = new BuildCommand(testProject, output: outputDir, framework: DefaultFramework);
            var result = buildCommand.ExecuteWithCapturedOutput();
            result.Should().Pass();

            result = Command.Create(Path.Combine(outputDir, "AppWithContentPackage" + buildCommand.GetExecutableExtension()), new string [0])
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();
            result.Should().Pass();

            // verify the output xml file
            new DirectoryInfo(outputDir).Sub("scripts").Should()
                .Exist()
                .And.HaveFile("run.cmd");
            new DirectoryInfo(outputDir).Should()
                .HaveFile("config.xml");
            // verify embedded resources
            result.StdOut.Should().Contain("AppWithContentPackage.dnf.png");
            result.StdOut.Should().Contain("AppWithContentPackage.ui.png");
            // verify 'all' language files not included
            result.StdOut.Should().NotContain("AppWithContentPackage.dnf_all.png");
            result.StdOut.Should().NotContain("AppWithContentPackage.ui_all.png");
            // verify classes
            result.StdOut.Should().Contain("AppWithContentPackage.Foo");
            result.StdOut.Should().Contain("MyNamespace.Util");
        }

        [Fact]
        public void EmbeddedResourcesAreCopied()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("EndToEndTestApp")
                                                .WithLockFiles()
                                                .WithBuildArtifacts();

            var root = testInstance.TestRoot;

            // run compile
            var outputDir = Path.Combine(root, "bin");
            var testProject = ProjectUtils.GetProjectJson(root, "EndToEndTestApp");
            var buildCommand = new BuildCommand(testProject, output: outputDir, framework: DefaultFramework);
            var result = buildCommand.ExecuteWithCapturedOutput();
            result.Should().Pass();

            var objDirInfo = new DirectoryInfo(Path.Combine(root, "obj", "Debug", DefaultFramework));
            objDirInfo.Should().HaveFile("EndToEndTestApp.resource1.resources");
            objDirInfo.Should().HaveFile("myresource.resources");
            objDirInfo.Should().HaveFile("EndToEndTestApp.defaultresource.resources");
        }

        [Fact]
        public void CopyToOutputFilesAreCopied()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("EndToEndTestApp")
                                                .WithLockFiles()
                                                .WithBuildArtifacts();

            var root = testInstance.TestRoot;

            // run compile
            var outputDir = Path.Combine(root, "bin");
            var testProject = ProjectUtils.GetProjectJson(root, "EndToEndTestApp");
            var buildCommand = new BuildCommand(testProject, output: outputDir, framework: DefaultFramework);
            var result = buildCommand.ExecuteWithCapturedOutput();
            result.Should().Pass();

            var outputDirInfo = new DirectoryInfo(Path.Combine(outputDir, "copy"));
            outputDirInfo.Should().HaveFile("file.txt");
            outputDirInfo.Should().NotHaveFile("fileex.txt");
        }

        [Fact]
        public void CanSetOutputAssemblyNameForLibraries()
        {
            var testInstance =
                TestAssetsManager
                    .CreateTestInstance("LibraryWithOutputAssemblyName")
                    .WithLockFiles();

            var root = testInstance.TestRoot;
            var outputDir = Path.Combine(root, "bin");
            var testProject = ProjectUtils.GetProjectJson(root, "LibraryWithOutputAssemblyName");
            var buildCommand = new BuildCommand(testProject, output: outputDir, framework: DefaultLibraryFramework);
            var result = buildCommand.ExecuteWithCapturedOutput();
            result.Should().Pass();

            new DirectoryInfo(outputDir).Should().HaveFiles(new [] { "MyLibrary.dll" });
        }

        [Fact]
        public void CanSetOutputAssemblyNameForApps()
        {
            var testInstance =
                TestAssetsManager
                    .CreateTestInstance("AppWithOutputAssemblyName")
                    .WithLockFiles();

            var root = testInstance.TestRoot;
            var outputDir = Path.Combine(root, "bin");
            var testProject = ProjectUtils.GetProjectJson(root, "AppWithOutputAssemblyName");
            var buildCommand = new BuildCommand(testProject, output: outputDir, framework: DefaultFramework);
            var result = buildCommand.ExecuteWithCapturedOutput();
            result.Should().Pass();

            new DirectoryInfo(outputDir).Should().HaveFiles(
                new [] { "MyApp.dll", "MyApp" + buildCommand.GetExecutableExtension(),
                    "MyApp.runtimeconfig.json", "MyApp.deps.json" });
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
