// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;
using System;

namespace Microsoft.DotNet.Tools.Publish.Tests
{
    public class PublishTests : TestBase
    {
        private readonly string _testProjectsRoot;
        private readonly Func<string, string, string> _getProjectJson = ProjectUtils.GetProjectJson;

        public PublishTests()
        {
            _testProjectsRoot = Path.Combine(RepoRoot, "TestAssets", "TestProjects");
        }

        public static IEnumerable<object[]> PublishOptions
        {
            get
            {
                return new[]
                {
                    new object[] { "1", "", "", "", "" },
                    new object[] { "2", "netstandardapp1.5", "", "", "" },
                    new object[] { "3", "", PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier(), "", "" },
                    new object[] { "4", "", "", "Release", "" },
                    new object[] { "5", "", "", "", "some/dir"},
                    new object[] { "6", "", "", "", "some/dir/with spaces" },
                    new object[] { "7", "netstandardapp1.5", PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier(), "Debug", "some/dir" },
                };
            }
        }

        [Theory]
        [MemberData("PublishOptions")]
        public void PublishOptionsTest(string testIdentifier, string framework, string runtime, string config, string outputDir)
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary", identifier: testIdentifier)
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            string testRoot = _getProjectJson(instance.TestRoot, "TestApp");

            outputDir = string.IsNullOrEmpty(outputDir) ? "" : Path.Combine(instance.TestRoot, outputDir);
            var publishCommand = new PublishCommand(testRoot, output: outputDir);
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
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithContents")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            var testProject = _getProjectJson(instance.TestRoot, "TestAppWithContents");
            var publishCommand = new PublishCommand(testProject);

            publishCommand.Execute().Should().Pass();
            publishCommand.GetOutputDirectory().Should().HaveFile("testcontentfile.txt");
        }

        [Fact]
        public void FailWhenNoRestoreTest()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary");

            string testProject = _getProjectJson(instance.TestRoot, "TestApp");
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Fail();
        }

        [Fact]
        public void LibraryPublishTest()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance(Path.Combine("TestAppWithLibrary"))
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            var testProject = _getProjectJson(instance.TestRoot, "TestLibrary");
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
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestBindingRedirectGeneration")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            var lesserTestProject = _getProjectJson(instance.TestRoot, "TestLibraryLesser");

            var publishCommand = new PublishCommand(lesserTestProject, "net451");
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.pdb");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.dll.config");
            publishCommand.GetOutputDirectory().Should().NotHaveFile("TestLibraryLesser.deps");
            publishCommand.GetOutputDirectory().Should().NotHaveFile("TestLibraryLesser.deps.json");

            // dependencies should also be copied
            publishCommand.GetOutputDirectory().Should().HaveFile("Newtonsoft.Json.dll");
            publishCommand.GetOutputDirectory().Delete(true);

            publishCommand = new PublishCommand(lesserTestProject, "netstandardapp1.5", PlatformServices.Default.Runtime.GetLegacyRestoreRuntimeIdentifier());
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.dll");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.pdb");
            publishCommand.GetOutputDirectory().Should().NotHaveFile("TestLibraryLesser.dll.config");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.deps");
            publishCommand.GetOutputDirectory().Should().HaveFile("TestLibraryLesser.deps.json");

            // dependencies should also be copied
            publishCommand.GetOutputDirectory().Should().HaveFile("Newtonsoft.Json.dll");
        }

        [Fact]
        public void RefsPublishTest()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppCompilationContext")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            var testProject = _getProjectJson(instance.TestRoot, "TestApp");
            var publishCommand = new PublishCommand(testProject);
            publishCommand.Execute().Should().Pass();

            publishCommand.GetOutputDirectory().Should().HaveFile("TestApp.dll");
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
            TestInstance instance = TestAssetsManager.CreateTestInstance("CompileFail")
                                                     .WithLockFiles();

            var testProject = _getProjectJson(instance.TestRoot, "CompileFail");
            var publishCommand = new PublishCommand(testProject);

            publishCommand.Execute().Should().Fail();
        }

        [Fact]
        public void PublishScriptsRun()
        {
            TestInstance instance = TestAssetsManager.CreateTestInstance("TestAppWithScripts")
                                                     .WithLockFiles()
                                                     .WithBuildArtifacts();

            var testProject = _getProjectJson(instance.TestRoot, "TestAppWithScripts");

            var publishCommand = new PublishCommand(testProject);
            var result = publishCommand.ExecuteWithCapturedOutput();

            result.Should().HaveStdOutMatching("\nprepublish_output( \\?[^%]+\\?){5}.+\npostpublish_output( \\?[^%]+\\?){5}", RegexOptions.Singleline);
            result.Should().Pass();
        }
    }
}
