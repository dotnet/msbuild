// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Publish.Tests
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
                "Newtonsoft.Json.dll",
                "System.Runtime.Serialization.Primitives.dll",
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

            // Ensure Newtonsoft.Json doesn't get excluded from the deps.json file.
            // TestLibrary has a hard dependency on Newtonsoft.Json.
            // TestApp has a PrivateAssets=All dependency on Microsoft.Extensions.DependencyModel, which depends on Newtonsoft.Json.
            // This verifies that P2P references get walked correctly when doing PrivateAssets exclusion.
            using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, "TestApp.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);
                dependencyContext
                    .RuntimeLibraries
                    .FirstOrDefault(l => l.Name == "newtonsoft.json")
                    .Should()
                    .NotBeNull();
            }
        }
    }
}
