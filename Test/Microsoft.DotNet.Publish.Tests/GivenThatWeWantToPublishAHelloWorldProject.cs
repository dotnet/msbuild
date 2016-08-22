// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
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
        public void It_publishes_the_project_to_the_publish_folder_and_the_app_should_run()
        {
            var packagesDirectory =
                Path.Combine(RepoInfo.RepoRoot, "bin", RepoInfo.Configuration, "Packages");
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld")
                .WithSource()
                .Restore("--fallbacksource", $"{packagesDirectory}");

            var publishCommand = new PublishCommand(Stage0MSBuild, helloWorldAsset.TestRoot);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().OnlyHaveFiles(new [] {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json"
            });

            Command.Create(RepoInfo.DotNetHostPath, new[] { Path.Combine(publishDirectory.FullName, "HelloWorld.dll") })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }
    }
}