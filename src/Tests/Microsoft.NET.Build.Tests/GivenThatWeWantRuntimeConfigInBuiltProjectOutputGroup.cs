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
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
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
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            new BuildCommand(testAsset)
                .Execute("/property:Configuration=Release")
                .Should()
                .Pass();

            var configFile = Path.Combine(testAsset.TestRoot, testProject.Name, "bin", "Release", testProject.TargetFrameworks, testProject.RuntimeIdentifier, testProject.Name + ".runtimeconfig.json");

            File.Exists(configFile).Should().BeTrue();
            File.ReadAllText(configFile).Should().NotContain("\"System.Runtime.TieredCompilation\"");
            File.ReadAllText(configFile).Should().NotContain("\"System.GC.Concurrent\"");
            File.ReadAllText(configFile).Should().NotContain("\"System.Threading.ThreadPool.MinThreads\"");

            testAsset = testAsset.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                var propertyGroup = new XElement(ns + "PropertyGroup");
                project.Root.Add(propertyGroup);
                propertyGroup.Add(new XElement(ns + "TieredCompilation", "false"));
                propertyGroup.Add(new XElement(ns + "ConcurrentGarbageCollection", "false"));
                propertyGroup.Add(new XElement(ns + "ThreadPoolMinThreads", "2"));
            });

            new BuildCommand(testAsset)
                .Execute("/property:Configuration=Release")
                .Should()
                .Pass();

            File.Exists(configFile).Should().BeTrue();
            File.ReadAllText(configFile).Should().Contain("\"System.Runtime.TieredCompilation\": false");
            File.ReadAllText(configFile).Should().Contain("\"System.GC.Concurrent\": false");
            File.ReadAllText(configFile).Should().Contain("\"System.Threading.ThreadPool.MinThreads\": 2");
        }

        [Fact]
        public void It_updates_runtime_config_properties_after_partial_build()
        {
            var testProject = new TestProject()
            {
                Name = "UpdateRuntimeConfigPartialBuild",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
                RuntimeIdentifier = $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x86"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            testAsset = testAsset.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                var propertyGroup = new XElement(ns + "PropertyGroup");
                project.Root.Add(propertyGroup);
                propertyGroup.Add(new XElement(ns + "TieredCompilation", "true"));
                propertyGroup.Add(new XElement(ns + "ConcurrentGarbageCollection", "true"));
                propertyGroup.Add(new XElement(ns + "ThreadPoolMinThreads", "3"));
            });

            new BuildCommand(testAsset)
                .Execute("/property:Configuration=Release")
                .Should()
                .Pass();

            var configFile = Path.Combine(testAsset.TestRoot, testProject.Name, "bin", "Release", testProject.TargetFrameworks, testProject.RuntimeIdentifier, testProject.Name + ".runtimeconfig.json");

            File.Exists(configFile).Should().BeTrue();
            File.ReadAllText(configFile).Should().Contain("\"System.Runtime.TieredCompilation\": true");
            File.ReadAllText(configFile).Should().Contain("\"System.GC.Concurrent\": true");
            File.ReadAllText(configFile).Should().Contain("\"System.Threading.ThreadPool.MinThreads\": 3");

            testAsset = testAsset.WithProjectChanges(project =>
            {
                var ns = project.Root.Name.Namespace;
                var propertyGroup = new XElement(ns + "PropertyGroup");
                project.Root.Add(propertyGroup);
                propertyGroup.Add(new XElement(ns + "TieredCompilation", "false"));
                propertyGroup.Add(new XElement(ns + "ConcurrentGarbageCollection", "false"));
                propertyGroup.Add(new XElement(ns + "ThreadPoolMinThreads", "2"));
            });

            new BuildCommand(testAsset)
                .Execute("/property:Configuration=Release")
                .Should()
                .Pass();

            File.Exists(configFile).Should().BeTrue();
            File.ReadAllText(configFile).Should().Contain("\"System.Runtime.TieredCompilation\": false");
            File.ReadAllText(configFile).Should().Contain("\"System.GC.Concurrent\": false");
            File.ReadAllText(configFile).Should().Contain("\"System.Threading.ThreadPool.MinThreads\": 2");
        }
    }
}
