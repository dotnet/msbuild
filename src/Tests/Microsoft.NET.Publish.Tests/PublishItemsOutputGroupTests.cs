using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class PublishItemsOutputGroupTests : SdkTest
    {
        public PublishItemsOutputGroupTests(ITestOutputHelper log) : base(log)
        {
        }

        private readonly static List<string> FrameworkAssemblies = new List<string>()
        {
            "api-ms-win-core-console-l1-1-0.dll",
            "System.Runtime.dll",
            "WindowsBase.dll",
        };

        [Fact]
        public void GroupPopulatedWithRid()
        {
            var testProject = this.SetupProject();
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(Log, testAsset.Path, testProject.Name);
            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(Log, testAsset.Path, testProject.Name);
            buildCommand
                .Execute("/p:RuntimeIdentifier=win-x86", "/t:PublishItemsOutputGroup")
                .Should()
                .Pass();

            var testOutputDir = new DirectoryInfo(Path.Combine(testAsset.Path, testProject.Name, "TestOutput"));
            Log.WriteLine("Contents of PublishItemsOutputGroup dumped to '{0}'.", testOutputDir.FullName);

            // Check for the existence of a few specific files that should be in the directory where the 
            // contents of PublishItemsOutputGroup were dumped to make sure it's getting populated.
            testOutputDir.Should().HaveFile($"{testProject.Name}.exe");
            testOutputDir.Should().HaveFile($"{testProject.Name}.deps.json");
            testOutputDir.Should().HaveFiles(FrameworkAssemblies);
        }

        [Fact]
        public void GroupNotPopulatedWithoutRid()
        {
            var testProject = this.SetupProject();
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(Log, testAsset.Path, testProject.Name);
            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(Log, testAsset.Path, testProject.Name);
            buildCommand
                .Execute("/t:PublishItemsOutputGroup")
                .Should()
                .Pass();

            var testOutputDir = new DirectoryInfo(Path.Combine(testAsset.Path, testProject.Name, "TestOutput"));
            Log.WriteLine("Contents of PublishItemsOutputGroup dumped to '{0}'.", testOutputDir.FullName);

            // Since no RID was specified the output group should only contain framework dependent output
            testOutputDir.Should().HaveFile($"{testProject.Name}{Constants.ExeSuffix}");
            testOutputDir.Should().HaveFile($"{testProject.Name}.deps.json");
            testOutputDir.Should().NotHaveFiles(FrameworkAssemblies);
        }

        private TestProject SetupProject()
        {
            var testProject = new TestProject()
            {
                Name = "TestPublishOutputGroup",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = "win-x86";

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            // Add a target that will dump the contents of the PublishItemsOutputGroup to
            // a test directory after building.
            testProject.CopyFilesTargets.Add(new CopyFilesTarget(
                "CopyPublishItemsOutputGroup",
                "PublishItemsOutputGroup",
                "@(PublishItemsOutputGroupOutputs)",
                "$(MSBuildProjectDirectory)\\TestOutput"));

            return testProject;
        }
    }
}
