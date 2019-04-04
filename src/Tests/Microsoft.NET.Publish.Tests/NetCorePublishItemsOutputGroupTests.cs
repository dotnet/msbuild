using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class NetCorePublishItemsOutputGroupTests : SdkTest
    {
        public NetCorePublishItemsOutputGroupTests(ITestOutputHelper log) : base(log)
        {
        }

        private static List<string> FrameworkAssemblies = new List<string>()
        {
            "api-ms-win-core-console-l1-1-0.dll",
            "System.Runtime.dll",
            "WindowsBase.dll",
        };

        [CoreMSBuildOnlyFact]
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
                .Execute("/p:RuntimeIdentifier=win-x86", "/t:NetCorePublishItemsOutputGroup")
                .Should()
                .Pass();

            var testOutputDir = Path.Combine(testAsset.Path, testProject.Name, "TestOutput");
            Log.WriteLine("Contents of NetCorePublishItemsOutputGroup dumped to '{0}'.", testOutputDir);

            // Check for the existence of a few specific files that should be in the directory where the 
            // contents of NetCorePublishItemsOutputGroup were dumped to make sure it's getting populated.
            Assert.True(File.Exists(Path.Combine(testOutputDir, $"{testProject.Name}.exe")), $"Assembly {testProject.Name}.exe is present in the output group.");
            foreach (var assem in FrameworkAssemblies)
            {
                Assert.True(File.Exists(Path.Combine(testOutputDir, assem)), $"Assembly {assem} is present in the output group.");
            }
        }

        [CoreMSBuildOnlyFact]
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
                .Execute("/t:NetCorePublishItemsOutputGroup")
                .Should()
                .Pass();

            var testOutputDir = Path.Combine(testAsset.Path, testProject.Name, "TestOutput");
            Log.WriteLine("Contents of NetCorePublishItemsOutputGroup dumped to '{0}'.", testOutputDir);

            // Since no RID was specified the output group should only contain framework dependent output
            Assert.True(File.Exists(Path.Combine(testOutputDir, $"{testProject.Name}.exe")), $"Assembly {testProject.Name}.exe is present in the output group.");
            foreach (var assem in FrameworkAssemblies)
            {
                Assert.False(File.Exists(Path.Combine(testOutputDir, assem)), $"Assembly {assem} is not present in the output group.");
            }
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

            // Add a target that will dump the contents of the NetCorePublishItemsOutputGroup to
            // a test directory after building.
            testProject.CopyFilesTargets.Add(new CopyFilesTarget(
                "CopyNetCorePublishItemsOutputGroup",
                "NetCorePublishItemsOutputGroup",
                "@(NetCorePublishItemsOutputGroupOutputs)",
                "$(MSBuildProjectDirectory)\\TestOutput"));

            return testProject;
        }
    }
}
