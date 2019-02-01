// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
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
                .WithTargetFramework(targetFramework)
                .Restore(Log);

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
    }
}
