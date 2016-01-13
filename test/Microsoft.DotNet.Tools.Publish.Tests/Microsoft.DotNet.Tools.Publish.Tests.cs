// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public class PublishTests : TestBase
    {
        private string _testProjectsRoot = @"TestProjects";

        public static IEnumerable<object[]> PublishOptions
        {
            get
            {
                return new[]
                {
                    new object[] { "", "", "", "" },
                    new object[] { "dnxcore50", "", "", "" },
                    new object[] { "", PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier(), "", "" },
                    new object[] { "", "", "Release", "" },
                    new object[] { "", "", "", "some/dir"},
                    //new object[] { "", "", "", "\"some/dir/with spaces\"" }, // issue - https://github.com/dotnet/cli/issues/525
                    new object[] { "dnxcore50", PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier(), "Debug", "some/dir" },
                };
            }
        }

        [Theory]
        [MemberData("PublishOptions")]
        public void PublishOptionsTest(string framework, string runtime, string config, string outputDir)
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();
            root.CopyFile(Path.Combine(_testProjectsRoot, "global.json"));
            var testAppDir = root.CreateDirectory("TestApp");
            var testLibDir = root.CreateDirectory("TestLibrary");

            //copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestApp"), testAppDir);
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibrary"), testLibDir);

            RunRestore(testAppDir.Path);
            RunRestore(testLibDir.Path);

            // run publish
            outputDir = string.IsNullOrEmpty(outputDir) ? "" : Path.Combine(root.Path, outputDir);
            var testProject = GetProjectPath(testAppDir);
            var publishCommand = new PublishCommand(testProject, output: outputDir);
            publishCommand.Execute().Should().Pass();

            // verify the output executable generated
            var publishedDir = publishCommand.GetOutputDirectory();
            var outputExe = publishCommand.GetOutputExecutable();
            var outputPdb = Path.ChangeExtension(outputExe, "pdb");

            // lets make sure that the output exe is runnable
            var outputExePath = Path.Combine(publishedDir.FullName, publishCommand.GetOutputExecutable());
            var command = new TestCommand(outputExePath);
            command.Execute("").Should().ExitWith(100);

            // the pdb should also be published
            publishedDir.Should().HaveFile(outputPdb);
        }

        [Fact]
        [ActiveIssue(491)]
        public void ProjectWithContentsTest()
        {
            // create unique directories in the 'temp' folder
            var testDir = Temp.CreateDirectory();
            testDir.CopyFile(Path.Combine(_testProjectsRoot, "global.json"));
            var testAppDir = Path.Combine(_testProjectsRoot, "TestAppWithContents");

            // copy projects to the temp dir
            CopyProjectToTempDir(testAppDir, testDir);

            RunRestore(testDir.Path);

            // run publish
            var testProject = GetProjectPath(testDir);
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Pass();

            // make sure that the output dir has the content files
            publishCommand.GetOutputDirectory().Should().HaveFile("testcontentfile.txt");
        }

        [Fact]
        public void BeforeRestoreTest()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();
            root.CopyFile(Path.Combine(_testProjectsRoot, "global.json"));
            var testAppDir = root.CreateDirectory("TestApp");
            var testLibDir = root.CreateDirectory("TestLibrary");

            // copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestApp"), testAppDir);
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibrary"), testLibDir);

            var testProject = GetProjectPath(testAppDir);
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Fail();
        }

        [Fact]
        public void LibraryPublishTest()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();
            root.CopyFile(Path.Combine(_testProjectsRoot, "global.json"));
            var testLibDir = root.CreateDirectory("TestLibrary");

            //copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibrary"), testLibDir);

            RunRestore(testLibDir.Path);

            var testProject = GetProjectPath(testLibDir);
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().NotHaveFile("TestLibrary.exe");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibrary.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibrary.pdb");
            // dependencies should also be copied
            publishCommand.GetOutputDirectory().Should().HaveFile("System.Runtime.dll");
        }

        [WindowsOnlyFact]
        public void TestLibraryPublishTest()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();
            var testLibDir = root.CreateDirectory("TestLibraryWithRunner");

            //copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibraryWithRunner"), testLibDir);

            RunRestore(testLibDir.Path);

            var testProject = GetProjectPath(testLibDir);
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryWithRunner.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryWithRunner.pdb");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryWithRunner.deps");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryWithRunner.dll.config");
            // dependencies should also be copied
            publishCommand.GetOutputDirectory().Should().HaveFile("Newtonsoft.Json.dll");
        }

        [Fact]
        public void CompilationFailedTest()
        {
            var testDir = Temp.CreateDirectory();
            testDir.CopyFile(Path.Combine(_testProjectsRoot, "global.json"));
            var compileFailDir = Path.Combine(_testProjectsRoot, "CompileFail");

            CopyProjectToTempDir(compileFailDir, testDir);

            RunRestore(testDir.Path);

            var testProject = GetProjectPath(testDir);
            var publishCommand = new PublishCommand(testProject);

            publishCommand.Execute().Should().Fail();
        }

        [Fact]
        public void PublishScriptsRun()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();
            root.CopyFile(Path.Combine(_testProjectsRoot, "global.json"));
            var testAppDir = root.CreateDirectory("TestApp");
            var testLibDir = root.CreateDirectory("TestLibrary");

            //copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestApp"), testAppDir);
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibrary"), testLibDir);

            RunRestore(testAppDir.Path);
            RunRestore(testLibDir.Path);

            // run publish
            var testProject = GetProjectPath(testAppDir);
            var publishCommand = new PublishCommand(testProject);

            var result = publishCommand.ExecuteWithCapturedOutput();

            result.Should().StdOutMatchPattern("\nprepublish_output( \\?[^%]+\\?){5}.+\npostpublish_output( \\?[^%]+\\?){5}", RegexOptions.Singleline);
            result.Should().Pass();
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
