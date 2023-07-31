// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToPublishACppCliAppProject : SdkTest
    {
        public GivenThatWeWantToPublishACppCliAppProject(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyFact]
        public void It_should_fail_with_error_message()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NETCoreCppClApp")
                .WithSource();

            new PublishCommand(Log, Path.Combine(testAsset.TestRoot, "NETCoreCppCliTest.sln"))
                .Execute("/p:NoBuild=true")
                .Should()
                .Fail()
                .And.HaveStdOutContaining(Strings.NoSupportCppNonDynamicLibraryDotnetCore);
        }
    }
}
