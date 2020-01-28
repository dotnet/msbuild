// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantRuntimeConfigInBuiltProjectOutputGroup : SdkTest
    {
        public GivenThatWeWantRuntimeConfigInBuiltProjectOutputGroup(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("netcoreapp1.1")]
        [InlineData("netcoreapp3.0")]
        public void It_has_target_path_and_final_outputput_path_metadata(string targetFramework)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", identifier: targetFramework)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var command = new GetValuesCommand(
                Log,
                testAsset.TestRoot,
                targetFramework,
                "BuiltProjectOutputGroupOutput",
                GetValuesCommand.ValueType.Item)
            {
                MetadataNames = { "FinalOutputPath", "TargetPath" },
                DependsOnTargets = "BuiltProjectOutputGroup",
            };

            command.Execute().Should().Pass();

            var outputDirectory = command.GetOutputDirectory(targetFramework);
            var runtimeConfigFile = outputDirectory.File("HelloWorld.runtimeconfig.json");
            var (_, metadata) = command.GetValuesWithMetadata().Single(i => i.value == runtimeConfigFile.FullName);

            metadata.Count.Should().Be(2);
            metadata.Should().Contain(KeyValuePair.Create("FinalOutputPath", runtimeConfigFile.FullName));
            metadata.Should().Contain(KeyValuePair.Create("TargetPath", runtimeConfigFile.Name));
        }

        [Fact]
        public void It_has_runtime_config_properties_after_partial_build()
        {
            var testProject = new TestProject()
            {
                Name = "RuntimeConfigPartialBuild",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp3.0",
                IsExe = true,
                RuntimeIdentifier = "win-x86"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute("/property:Configuration=Release")
                .Should()
                .Pass();

            var runtimeConfigFile = Path.Combine(testAsset.TestRoot, testProject.Name, "bin", "Release", testProject.TargetFrameworks, testProject.RuntimeIdentifier, testProject.Name + ".runtimeconfig.json");
            File.Exists(runtimeConfigFile).Should().BeTrue();
            File.ReadAllText(runtimeConfigFile).Should().NotContain("TieredCompilation");

            testAsset = testAsset.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                var propertyGroup = new XElement(ns + "PropertyGroup");
                project.Root.Add(propertyGroup);
                propertyGroup.Add(new XElement(ns + "TieredCompilation", "false"));
            });

            new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute("/property:Configuration=Release")
                .Should()
                .Pass();

            File.Exists(runtimeConfigFile).Should().BeTrue();
            File.ReadAllText(runtimeConfigFile).Should().Contain("TieredCompilation");
        }
    }
}
