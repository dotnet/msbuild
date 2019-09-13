// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit.Abstractions;

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
                .WithSource()
                .Restore(Log, "NETCoreCppCliTest.sln");

            WorkaroundSDKBlockOnAssetsJsonExistence(testAsset);

            new PublishCommand(Log, Path.Combine(testAsset.TestRoot, "CSConsoleApp"))
                .Execute(new string[] { "-p:Platform=x64" })
                .Should()
                .Pass();

            var exe = Path.Combine( //find the platform directory
                new DirectoryInfo(Path.Combine(testAsset.TestRoot, "CSConsoleApp", "bin")).GetDirectories().Single().FullName,
                "Debug",
                "netcoreapp3.0",
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

        [FullMSBuildOnlyFact]
        public void When_not_referenced_by_csharp_project_it_fails_to_publish()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("NetCoreCsharpAppReferenceCppCliLib")
                .WithSource()
                .Restore(Log, "NETCoreCppCliTest.sln");

            WorkaroundSDKBlockOnAssetsJsonExistence(testAsset);

            new PublishCommand(Log, Path.Combine(testAsset.TestRoot, "NETCoreCppCliTest"))
                .Execute(new string[] { "-p:Platform=x64" })
                .Should()
                .Fail()
                .And.HaveStdOutContaining(Strings.NoSupportCppPublishDotnetCore);
        }

        private static void WorkaroundSDKBlockOnAssetsJsonExistence(TestAsset testAsset)
        {
            var lockFile = new LockFile();
            lockFile.Targets.Add(new LockFileTarget { TargetFramework = NuGetFramework.Parse(".NETCoreApp,Version=v3.0") });

            var objDirectory = Directory.CreateDirectory(Path.Combine(testAsset.TestRoot, "NETCoreCppCliTest", "obj"));
            new LockFileFormat().Write(objDirectory.File("project.assets.json").FullName, lockFile);
        }
    }
}
