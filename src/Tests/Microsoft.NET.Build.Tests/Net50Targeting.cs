// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class Net50Targeting : SdkTest
    {
        public Net50Targeting(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionFact("16.8.0")]
        public void Net50TargetFrameworkParsesAsNetCoreAppTargetFrameworkIdentifier()
        {
            var testProject = new TestProject()
            {
                Name = "Net5Test",
                TargetFrameworks = "net5.0",
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, testProject.Name);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand.Execute()
                .Should()
                .Pass();

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name), testProject.TargetFrameworks, "TargetFrameworkIdentifier");
            getValuesCommand.Execute()
                .Should()
                .Pass();

            getValuesCommand.GetValues().Should().BeEquivalentTo(".NETCoreApp");
        }
    }
}
