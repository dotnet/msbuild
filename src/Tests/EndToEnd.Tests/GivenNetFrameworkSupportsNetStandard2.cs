// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace EndToEnd
{
    public class GivenNetFrameworkSupportsNetStandard2 : SdkTest
    {
        public GivenNetFrameworkSupportsNetStandard2(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void Anet462ProjectCanReferenceANETStandardProject()
        {
            var _testInstance = _testAssetsManager.CopyTestAsset("NETFrameworkReferenceNETStandard20", testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
                .WithSource();

            string projectDirectory = Path.Combine(_testInstance.Path, "TestApp");

            new BuildCommand(Log, projectDirectory)
                .Execute()
                .Should().Pass();

            new DotnetCommand(Log, "run")
                    .WithWorkingDirectory(projectDirectory)
                    .Execute()
                    .Should().Pass()
                         .And.HaveStdOutContaining("This string came from the test library!");

        }
    }
}
