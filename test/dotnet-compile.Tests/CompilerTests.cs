// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Compiler.Tests
{
    public class CompilerTests : TestBase
    {
        private readonly string _testProjectsRoot;

        public CompilerTests()
        {
            _testProjectsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects");
        }

        [Fact]
        public void XmlDocumentationFileIsGenerated()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();
            root.CopyFile(Path.Combine(_testProjectsRoot, "global.json"));

            var testLibDir = root.CreateDirectory("TestLibrary");
            var sourceTestLibDir = Path.Combine(_testProjectsRoot, "TestAppWithLibrary", "TestLibrary");

            CopyProjectToTempDir(sourceTestLibDir, testLibDir);

            // run compile
            var outputDir = Path.Combine(testLibDir.Path, "bin");
            var testProject = GetProjectPath(testLibDir);
            var buildCommand = new BuildCommand(testProject, output: outputDir, framework: DefaultFramework);
            var result = buildCommand.ExecuteWithCapturedOutput();
            result.Should().Pass();

            // verify the output xml file
            var outputXml = Path.Combine(outputDir, "Debug", DefaultFramework, "TestLibrary.xml");
            Console.WriteLine("OUTPUT XML PATH: " + outputXml);
            Assert.True(File.Exists(outputXml));
            Assert.Contains("Gets the message from the helper", File.ReadAllText(outputXml));
        }

        [Fact]
        public void SatelliteAssemblyIsGeneratedByDotnetBuild()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();
            var testLibDir = root.CreateDirectory("TestProjectWithCultureSpecificResource");
            var sourceTestLibDir = Path.Combine(_testProjectsRoot, "TestProjectWithCultureSpecificResource");

            CopyProjectToTempDir(sourceTestLibDir, testLibDir);

            // run compile on a project with resources
            var outputDir = Path.Combine(testLibDir.Path, "bin");
            var testProject = GetProjectPath(testLibDir);
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
            var root = Temp.CreateDirectory();
            var testLibDir = root.CreateDirectory("TestLibraryWithAnalyzer");
            var sourceTestLibDir = Path.Combine(_testProjectsRoot, "TestLibraryWithAnalyzer");

            CopyProjectToTempDir(sourceTestLibDir, testLibDir);
            
            // run compile
            var outputDir = Path.Combine(testLibDir.Path, "bin");
            var testProject = GetProjectPath(testLibDir);
            var buildCmd = new BuildCommand(testProject, output: outputDir, framework: DefaultFramework);
            var result = buildCmd.ExecuteWithCapturedOutput();
            result.Should().Pass();
            
            Assert.Contains("CA1018", result.StdErr);
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
