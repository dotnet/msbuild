// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAUnitTestProject : SdkTest
    {
        public GivenThatWeWantToBuildAUnitTestProject(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_generates_runtime_config()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("XUnitTestProject")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework);
            outputDirectory.Should().HaveFile(@"XUnitTestProject.runtimeconfig.json");
        }

        [Fact]
        public void It_builds_when_has_runtime_output_is_true()
        {
            const string targetFramework = "netcoreapp2.1";

            var testAsset = _testAssetsManager
                .CopyTestAsset("XUnitTestProject")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute(new string[] {
                    "/restore",
                    $"/p:TargetFramework={targetFramework}",
                    $"/p:HasRuntimeOutput=true",
                    $"/p:NETCoreSdkRuntimeIdentifier={EnvironmentInfo.GetCompatibleRid(targetFramework)}"
                })
                .Should()
                .Pass();
        }
    }
}