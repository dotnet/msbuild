using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class FilesCopiedToPublishDirTests : SdkTest
    {
        public FilesCopiedToPublishDirTests(ITestOutputHelper log) : base(log)
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
        public void RunFilesCopiedToPublishDirTest(bool specifyRid, bool singleFile)
        {
            var testProject = new TestProject()
            {
                Name = "TestFilesCopiedToPublishDir",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = "win-x86";
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";
            if (specifyRid)
            {
                testProject.AdditionalProperties["RuntimeIdentifier"] = "win-x86";
            }

            if (singleFile)
            {
                testProject.AdditionalProperties["PublishSingleFile"] = "true";
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(Log, testAsset.Path, testProject.Name);
            restoreCommand
                .Execute()
                .Should()
                .Pass();

            var command = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.Path, testProject.Name),
                testProject.TargetFrameworks,
                "FilesCopiedToPublishDir",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "ComputeFilesCopiedToPublishDir",
                MetadataNames = { "RelativePath", "IsKeyOutput" },
            };

            command.Execute().Should().Pass();
            var items = from item in command.GetValuesWithMetadata()
                        select new
                        {
                            Identity = item.value,
                            RelativePath = item.metadata["RelativePath"]
                        };

            Log.WriteLine("FilesCopiedToPublishDir contains '{0}' items:", items.Count());
            foreach (var item in items)
            {
                Log.WriteLine("    '{0}': RelativePath = '{1}'", item.Identity, item.RelativePath);
            }

            // Check for the main exe
            if (RuntimeEnvironment.OperatingSystemPlatform != Platform.Darwin)
            {
                string exeSuffix = specifyRid ? ".exe" : Constants.ExeSuffix;
                items.Should().ContainSingle(i => i.RelativePath.Equals($"{testProject.Name}{exeSuffix}", StringComparison.OrdinalIgnoreCase));
            }

            // Framework assemblies should be there if we specified and rid and this isn't in the single file case
            if (specifyRid && !singleFile)
            {
                FrameworkAssemblies.ForEach(fa => items.Should().ContainSingle(i => i.RelativePath.Equals(fa, StringComparison.OrdinalIgnoreCase)));
            }
            else
            {
                FrameworkAssemblies.ForEach(fa => items.Should().NotContain(i => i.RelativePath.Equals(fa, StringComparison.OrdinalIgnoreCase)));
            }

            // FilesCopiedToPublishDir should never contain the deps.json file 
            items.Should().NotContain(i => i.RelativePath.Equals($"{testProject.Name}.deps.json", StringComparison.OrdinalIgnoreCase));
        }
    }
}
