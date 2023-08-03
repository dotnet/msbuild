// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildASolutionWithProjRefDiffCase : SdkTest
    {
        public GivenThatWeWantToBuildASolutionWithProjRefDiffCase(ITestOutputHelper log) : base(log)
        {
        }

        [PlatformSpecificFact(TestPlatforms.Windows | TestPlatforms.OSX)]
        public void ItBuildsTheSolutionSuccessfully()
        {
            const string solutionFile = "AppWithProjRefCaseDiff.sln";

            var asset = _testAssetsManager
                .CopyTestAsset("AppWithProjRefCaseDiff")
                .WithSource();

            var command = new BuildCommand(asset, solutionFile);
            command.Execute().Should().Pass();
        }
    }
}
