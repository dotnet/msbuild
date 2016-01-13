// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public class CompilerTests : TestBase
    {
        private string _testProjectsRoot = @"TestProjects";

        [Fact]
        public void XmlDocumentationFileIsGenerated()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();
            var testLibDir = root.CreateDirectory("TestLibrary");

            // copy projects to the temp dir and restore them
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibrary"), testLibDir);
            RunRestore(testLibDir.Path);

            // run compile
            var outputDir = Path.Combine(testLibDir.Path, "bin");
            var testProject = GetProjectPath(testLibDir);
            var buildCommand = new BuildCommand(testProject, output: outputDir);
            var result = buildCommand.ExecuteWithCapturedOutput();
            result.Should().Pass();

            // verify the output xml file
            var outputXml = Path.Combine(outputDir, "TestLibrary.xml");
            Assert.True(File.Exists(outputXml));
            Assert.Contains("Gets the message from the helper", File.ReadAllText(outputXml));
        }

        private void CopyProjectToTempDir(string projectDir, TempDirectory tempDir)
        {
            // copy all the files to temp dir
            foreach (var file in Directory.EnumerateFiles(projectDir))
            {
                // never copy project.lock.json. All the tests are expected to call 'dotnet restore'
                if (file.ToLower().EndsWith("project.lock.json"))
                {
                    continue;
                }

                tempDir.CopyFile(file);
            }
        }

        private string GetProjectPath(TempDirectory projectDir)
        {
            return Path.Combine(projectDir.Path, "project.json");
        }

        private void RunRestore(string args)
        {
            var restoreCommand = new RestoreCommand();
            restoreCommand.Execute($"--quiet {args}").Should().Pass();
        }
    }
}
