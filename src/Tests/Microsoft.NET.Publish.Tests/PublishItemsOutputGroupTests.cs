// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

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

        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void RunPublishItemsOutputGroupTest(bool specifyRid, bool singleFile)
        {
            var testProject = this.SetupProject(specifyRid, singleFile);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: specifyRid.ToString() + singleFile.ToString());

            var restoreCommand = new RestoreCommand(testAsset);
            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var command = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.Path, testProject.Name),
                testProject.TargetFrameworks,
                "PublishItemsOutputGroupOutputs",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "PublishItemsOutputGroup",
                MetadataNames = { "TargetPath", "IsKeyOutput" },
            };

            command.Execute().Should().Pass();
            var items = from item in command.GetValuesWithMetadata()
                        select new
                        {
                            Identity = item.value,
                            TargetPath = item.metadata["TargetPath"],
                            IsKeyOutput = item.metadata["IsKeyOutput"]
                        };

            Log.WriteLine("PublishItemsOutputGroup contains '{0}' items:", items.Count());
            foreach (var item in items)
            {
                Log.WriteLine("    '{0}': TargetPath = '{1}', IsKeyOutput = '{2}'", item.Identity, item.TargetPath, item.IsKeyOutput);
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // Check that there's only one key item, and it's the exe
                string exeSuffix = specifyRid ? ".exe" : Constants.ExeSuffix;
                items.
                    Where(i => i.IsKeyOutput.Equals("true", StringComparison.OrdinalIgnoreCase)).
                    Should().
                    ContainSingle(i => i.TargetPath.Equals($"{testProject.Name}{exeSuffix}", StringComparison.OrdinalIgnoreCase));
            }

            // Framework assemblies should be there if we specified and rid and this isn't in the single file case
            if (specifyRid && !singleFile)
            {
                FrameworkAssemblies.ForEach(fa => items.Should().ContainSingle(i => i.TargetPath.Equals(fa, StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                FrameworkAssemblies.ForEach(fa => items.Should().NotContain(i => i.TargetPath.Equals(fa, StringComparison.OrdinalIgnoreCase)));
            }

            // The deps.json file should be included unless this is the single file case
            if (!singleFile)
            {
                items.Should().ContainSingle(i => i.TargetPath.Equals($"{testProject.Name}.deps.json", StringComparison.OrdinalIgnoreCase));
            }
        }

        [Fact]
        public void GroupBuildsWithoutPublish()
        {
            var testProject = this.SetupProject();
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(testAsset);
            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute("/p:RuntimeIdentifier=win-x86;DesignTimeBuild=true", "/t:PublishItemsOutputGroup")
                .Should()
                .Pass();

            // Confirm we were able to build the output group without the publish actually happening
            var publishDir = new DirectoryInfo(Path.Combine(buildCommand.GetOutputDirectory(testProject.TargetFrameworks).FullName, "win-x86", "publish"));
            publishDir
                .Should()
                .NotExist();
        }

        private TestProject SetupProject(bool specifyRid = true, bool singleFile = false)
        {
            var testProject = new TestProject()
            {
                Name = "TestPublishOutputGroup",
                TargetFrameworks = "net6.0",
                IsExe = true
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = "win-x86";

            // Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            // This target is primarily used during design time builds
            testProject.AdditionalProperties["DesignTimeBuild"] = "true";

            if (specifyRid)
            {
                testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x86";
            }

            if (singleFile)
            {
                testProject.AdditionalProperties["PublishSingleFile"] = "true";
            }

            return testProject;
        }
    }
}
