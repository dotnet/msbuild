// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NETCore.TestFramework;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Xunit;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NETCore.Publish.Tests
{
    public class GivenThatWeWantToPublishAProjectWithCopyToPublish
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
        public void It_publishes_projects_and_copies_items_correctly()
        {
            string packagesDirectory =
                Path.Combine(RepoInfo.RepoRoot, "bin", RepoInfo.Configuration, "Packages");
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("AppWithCopyToPublish")
                .WithSource()
                .Restore("--fallbacksource", $"{packagesDirectory}");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            PublishCommand publishCommand = new PublishCommand(Stage0MSBuild, appProjectDirectory);
            publishCommand
                .Execute()
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.dll",
                "TestApp.pdb",
                "TestApp.deps.json",
                "TestApp.runtimeconfig.json",
                "CompileCopyToOutput.cs",
                "Resource1.resx",
                "ContentAlways.txt",
                "ContentPreserveNewest.txt",
                "NoneCopyOutputAlways.txt",
                "NoneCopyOutputPreserveNewest.txt",
                "CopyToOutputFromProjectReference.txt",
            });
        }
    }
}