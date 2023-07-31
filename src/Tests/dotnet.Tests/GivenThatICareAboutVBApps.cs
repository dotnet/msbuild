// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests
{
    public class GivenThatICareAboutVBApps : SdkTest
    {
        public GivenThatICareAboutVBApps(ITestOutputHelper log) : base(log)
        {
        }


        [Fact]
        public void ICanBuildVBApps()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("VBTestApp")
                .WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ICanRunVBApps()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("VBTestApp")
                .WithSource();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("run")
                .Should().Pass();
        }

        [Fact]
        public void ICanPublicAndRunVBApps()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("VBTestApp")
                .WithSource();

            var publishCommand = new PublishCommand(testInstance);

            publishCommand
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputDll = Path.Combine(
                publishCommand.GetOutputDirectory(configuration: configuration).FullName,
                "VBTestApp.dll");

            new DotnetCommand(Log)
                .Execute(outputDll)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World");
        }
    }
}
