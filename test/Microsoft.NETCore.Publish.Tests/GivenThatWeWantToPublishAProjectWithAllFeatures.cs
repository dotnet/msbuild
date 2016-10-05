// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.NETCore.TestFramework;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Xunit;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NETCore.Publish.Tests
{
    public class GivenThatWeWantToPublishAProjectWithAllFeatures
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
        public void It_publishes_the_project_correctly()
        {
            TestAsset testAsset = _testAssetsManager
                .CopyTestAsset("KitchenSink")
                .WithSource();

            testAsset.Restore("TestApp");
            testAsset.Restore("TestLibrary");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            // Temporarily pass in the TFM to publish until https://github.com/dotnet/sdk/issues/175 is addressed
            PublishCommand publishCommand = new PublishCommand(Stage0MSBuild, appProjectDirectory);
            publishCommand
                .Execute("/p:TargetFramework=netcoreapp1.0")
                .Should()
                .Pass();

            DirectoryInfo publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.dll",
                "TestApp.pdb",
                "TestApp.deps.json",
                "TestApp.runtimeconfig.json",
                "TestLibrary.dll",
                "TestLibrary.pdb",
                "CompileCopyToOutput.cs",
                "Resource1.resx",
                "ContentAlways.txt",
                "ContentPreserveNewest.txt",
                "NoneCopyOutputAlways.txt",
                "NoneCopyOutputPreserveNewest.txt",
                "CopyToOutputFromProjectReference.txt",
            });

            var cultures = new List<string>() { "da", "de", "fr" };

            foreach (var culture in cultures)
            {
                var cultureDir = new DirectoryInfo(Path.Combine(publishDirectory.FullName, culture));
                cultureDir.Should().Exist();
                cultureDir.Should().HaveFile("TestApp.resources.dll");
                cultureDir.Should().HaveFile("TestLibrary.resources.dll");
            }
        }
    }
}
