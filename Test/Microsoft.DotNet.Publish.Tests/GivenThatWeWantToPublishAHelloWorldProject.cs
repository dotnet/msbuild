// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.TestFramework.Assertions;
using Microsoft.DotNet.TestFramework.Commands;
using Xunit;
using static Microsoft.DotNet.TestFramework.Commands.MSBuildTest;

namespace Microsoft.DotNet.Publish.Tests
{
    public class GivenThatWeWantToPublishAHelloWorldProject
    {
        private TestAssetsManager _testAssetsManager;

        public GivenThatWeWantToPublishAHelloWorldProject()
        {
            _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;
        }

        [Fact]
        public void It_publishes_the_project_binary_to_the_publish_folder()
        {
            var packagesDirectory = Path.Combine(RepoInfo.RepoRoot, "bin", "Debug", "Packages");
            var helloWorlAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .WithLockFile()
                .Restore($"/p:RestoreSources={packagesDirectory}");

            var publishCommand = new PublishCommand(Stage0MSBuild, helloWorlAsset.TestRoot);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().HaveFiles(new [] {
                "HelloWorld.dll",
                "HelloWorld.pdb"
            });
        }
    }
}