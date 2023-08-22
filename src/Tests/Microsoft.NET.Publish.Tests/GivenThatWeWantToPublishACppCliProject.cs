// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToPublishACppCliProject : SdkTest
    {
        public GivenThatWeWantToPublishACppCliProject(ITestOutputHelper log) : base(log)
        {
        }

        [FullMSBuildOnlyFact]
        public void When_referenced_by_csharp_project_it_publishes_and_runs()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource();

            new PublishCommand(Log, Path.Combine(testAsset.TestRoot, "CSConsoleApp"))
                .Execute(new string[] { "-p:Platform=x64", "-p:EnableManagedpackageReferenceSupport=true" })
                .Should()
                .Pass();

            var exe = Path.Combine( //find the platform directory
                new DirectoryInfo(Path.Combine(testAsset.TestRoot, "CSConsoleApp", "bin")).GetDirectories().Single().FullName,
                "Debug",
                $"{ToolsetInfo.CurrentTargetFramework}-windows",
                "publish",
                "CSConsoleApp.exe");

            var runCommand = new RunExeCommand(Log, exe);
            runCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello, World!");
        }

        [FullMSBuildOnlyFact(Skip = "There is no publish error when using PackageReference support which is required for testing")]
        public void When_not_referenced_by_csharp_project_it_fails_to_publish()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource();

            new PublishCommand(Log, Path.Combine(testAsset.TestRoot, "NETCoreCppCliTest"))
                .Execute(new string[] { "-p:Platform=x64", "-p:EnableManagedpackageReferenceSupport=true" })
                .Should()
                .Fail()
                .And.HaveStdOutContaining(Strings.NoSupportCppPublishDotnetCore);
        }
    }
}
