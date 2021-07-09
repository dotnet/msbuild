// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class MetadataUpdaterSupportTest : SdkTest
    {
        public MetadataUpdaterSupportTest(ITestOutputHelper log) : base(log)
        {
        }

        [CoreMSBuildOnlyFact] // Running on desktop causes failures attempting to restore M.NETCore.App.WinHost.
        public void It_Configures_MetadataUpdaterSupport_InReleaseBuilds()
        {
            var targetFramework = "net6.0";
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework("net6.0");

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute("/p:Configuration=Release")
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, configuration: "Release");
            var runtimeConfigPath = Path.Combine(outputDirectory.FullName, "HelloWorld.runtimeconfig.json");

            File.Exists(runtimeConfigPath).Should().BeTrue();

            var fileContents = File.ReadAllText(runtimeConfigPath);
            fileContents.Should().Contain("\"System.Reflection.Metadata.MetadataUpdater.IsSupported\": false");
        }

        [CoreMSBuildOnlyFact]
        public void It_Configures_MetadataUpdaterSupport_InDebugBuilds()
        {
            var targetFramework = "net6.0";
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFramework("net6.0");

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);
            var runtimeConfigPath = Path.Combine(outputDirectory.FullName, "HelloWorld.runtimeconfig.json");

            File.Exists(runtimeConfigPath).Should().BeTrue();

            var fileContents = File.ReadAllText(runtimeConfigPath);
            fileContents.Should().NotContain("\"System.Reflection.Metadata.MetadataUpdater.IsSupported\": false");
        }
    }
}
