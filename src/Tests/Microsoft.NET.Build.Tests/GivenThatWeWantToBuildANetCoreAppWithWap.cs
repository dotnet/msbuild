// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToBuildANetCoreAppWithWap : SdkTest
    {
        public GivenThatWeWantToBuildANetCoreAppWithWap(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyFact]
        public void WhenNetCoreProjectIsReferencedByAWapProject()
        {
            var testInstance = _testAssetsManager
                .CopyTestAsset("TestAppWithWapAndWpf")
                .WithSource();

            new BuildCommand(testInstance, "WapProjTemplate1")
                .Execute()
                .Should()
                .Pass();
        }
    }
}
