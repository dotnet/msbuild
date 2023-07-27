// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Pack.Tests
{
    public class SolutionPackTests : SdkTest
    {
        public SolutionPackTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItCanPackASolutionWithOutputPath()
        {
            var testProject1 = new TestProject()
            {
                Name = "Project1",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            var testProject2 = new TestProject()
            {
                Name = "Project2",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };

            var testAsset = _testAssetsManager.CreateTestProjects(new[] { testProject1, testProject2 });

            string packageOutputPath = Path.Combine(testAsset.Path, "output", "packages");

            new DotnetCommand(Log, "pack", "--output", packageOutputPath)
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            new FileInfo(Path.Combine(packageOutputPath, testProject1.Name + ".1.0.0.nupkg")).Should().Exist();
            new FileInfo(Path.Combine(packageOutputPath, testProject1.Name + ".1.0.0.nupkg")).Should().Exist();

        }
    }
}
