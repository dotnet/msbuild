// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NETCore.TestFramework;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Xunit;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NETCore.Publish.Tests
{
    public class GivenThatWeWantToPublishAProjectWithDependencies
    {
        private TestAssetsManager _testAssetsManager;

        public GivenThatWeWantToPublishAProjectWithDependencies()
        {
            _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;
        }

        [Fact]
        public void It_publishes_projects_with_simple_dependencies()
        {
            TestAsset simpleDependenciesAsset = _testAssetsManager
                .CopyTestAsset("SimpleDependencies")
                .WithSource()
                .Restore();

            // Temporarily pass in the TFM to publish until https://github.com/dotnet/sdk/issues/175 is addressed
            PublishCommand publishCommand = new PublishCommand(Stage0MSBuild, simpleDependenciesAsset.TestRoot);
            publishCommand
                .Execute("/p:TargetFramework=netcoreapp1.0")
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "SimpleDependencies.dll",
                "SimpleDependencies.pdb",
                "SimpleDependencies.deps.json",
                "SimpleDependencies.runtimeconfig.json",
                "Newtonsoft.Json.dll",
                "System.Runtime.Serialization.Primitives.dll",
                "System.Collections.NonGeneric.dll",
            });

            string appPath = publishCommand.GetPublishedAppPath("SimpleDependencies");

            Command runAppCommand = Command.Create(
                RepoInfo.DotNetHostPath,
                new[] { appPath, "one", "two" });

            string expectedOutput =
@"{
  ""one"": ""one"",
  ""two"": ""two""
}";

            runAppCommand
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining(expectedOutput);
        }
    }
}