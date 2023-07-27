// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAP2PReferenceWithTargetPlatform : SdkTest
    {
        public GivenThatWeWantToBuildAP2PReferenceWithTargetPlatform(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_successfully()
        {
            var appProject = new TestProject()
            {
                Name = "P2PrefernceWithTargetPlatform_App",
                TargetFrameworks = "net5-windows",
                IsExe = true
            };

            var libraryProject = new TestProject()
            {
                Name = "P2PrefernceWithTargetPlatform_App_Library",
                TargetFrameworks = "net5-windows",
            };

            appProject.ReferencedProjects.Add(libraryProject);

            var testAsset = _testAssetsManager.CreateTestProject(appProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
