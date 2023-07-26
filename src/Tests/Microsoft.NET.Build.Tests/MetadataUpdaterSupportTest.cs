// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
