// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;
using System;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public class PublishTests : TestBase
    {
        private readonly string _testProjectsRoot;

        public PublishTests()
        {
            _testProjectsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects");
        }

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
                    new object[] { "", "", "", "some/dir/with spaces" }, 
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

            var testAppDir = root.CreateDirectory("TestApp");
            var testLibDir = root.CreateDirectory("TestLibrary");

            //copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestApp"), testAppDir);
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibrary"), testLibDir);

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
        public void ProjectWithContentsTest()
        {
            // create unique directories in the 'temp' folder
            var testDir = Temp.CreateDirectory();

            var testAppDir = Path.Combine(_testProjectsRoot, "TestAppWithContents");

            // copy projects to the temp dir
            CopyProjectToTempDir(testAppDir, testDir);

            // run publish
            var testProject = GetProjectPath(testDir);
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Pass();

            // make sure that the output dir has the content files
            publishCommand.GetOutputDirectory().Should().HaveFile("testcontentfile.txt");
        }

        [Fact]
        public void FailWhenNoRestoreTest()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();

            var testAppDir = root.CreateDirectory("TestApp");
            var testLibDir = root.CreateDirectory("TestLibrary");

            // copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestApp"), testAppDir);
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibrary"), testLibDir);

            File.Delete(Path.Combine(testAppDir.Path, "project.lock.json"));
            File.Delete(Path.Combine(testLibDir.Path, "project.lock.json"));

            var testProject = GetProjectPath(testAppDir);
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Fail();
        }

        [Fact]
        public void LibraryPublishTest()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();

            var testLibDir = root.CreateDirectory("TestLibrary");

            //copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibrary"), testLibDir);

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
        public void TestLibraryBindingRedirectGeneration()
        {
            // Set up Test Staging in Temporary Directory
            var root = Temp.CreateDirectory();
            root.CopyDirectory(Path.Combine(_testProjectsRoot, "TestBindingRedirectGeneration"));

            var testProjectsRootDir = Path.Combine(root.Path, "TestBindingRedirectGeneration");
            var greaterTestLibDir = Path.Combine(testProjectsRootDir, "TestLibraryGreater");
            var lesserTestLibDir = Path.Combine(testProjectsRootDir, "TestLibraryLesser");

            var lesserTestProject = Path.Combine(lesserTestLibDir, "project.json");
            var publishCommand = new PublishCommand(lesserTestProject, "net451");
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.pdb");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.dll.config");
            publishCommand.GetOutputDirectory().Should().NotHaveFile("TestLibraryLesser.deps"); 

            // dependencies should also be copied
            publishCommand.GetOutputDirectory().Should().HaveFile("Newtonsoft.Json.dll");
            publishCommand.GetOutputDirectory().Delete(true);

            publishCommand = new PublishCommand(lesserTestProject, "dnxcore50", PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier());
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.pdb");
            publishCommand.GetOutputDirectory().Should().NotHaveFile("TestLibraryLesser.dll.config");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.deps"); 

            // dependencies should also be copied
            publishCommand.GetOutputDirectory().Should().HaveFile("Newtonsoft.Json.dll");
        }

        [Fact]
        public void RefsPublishTest()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();

            var testAppDir = root.CreateDirectory("TestAppCompilationContext");
            var testLibDir = root.CreateDirectory("TestLibrary");

            // copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestAppCompilationContext"), testAppDir);
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibrary"), testLibDir);

            var testProject = GetProjectPath(testAppDir);
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestAppCompilationContext.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibrary.dll");

            var refsDirectory = new DirectoryInfo(Path.Combine(publishCommand.GetOutputDirectory().FullName, "refs"));
            // Should have compilation time assemblies
            refsDirectory.Should().HaveFile("System.IO.dll");
            // Libraries in which lib==ref should be deduped
            refsDirectory.Should().NotHaveFile("TestLibrary.dll");
        }


        [Fact]
        public void CompilationFailedTest()
        {
            var testDir = Temp.CreateDirectory();

            var compileFailDir = Path.Combine(_testProjectsRoot, "CompileFail");

            CopyProjectToTempDir(compileFailDir, testDir);

            var testProject = GetProjectPath(testDir);
            var publishCommand = new PublishCommand(testProject);

            publishCommand.Execute().Should().Fail();
        }

        [Fact]
        public void PublishScriptsRun()
        {
            // create unique directories in the 'temp' folder
            var root = Temp.CreateDirectory();

            var testAppDir = root.CreateDirectory("TestAppWithScripts");

            //copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestAppWithScripts"), testAppDir);

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
                tempDir.CopyFile(file);
            }
        }

        private string GetProjectPath(TempDirectory projectDir)
        {
            return Path.Combine(projectDir.Path, "project.json");
        }
    }
}
