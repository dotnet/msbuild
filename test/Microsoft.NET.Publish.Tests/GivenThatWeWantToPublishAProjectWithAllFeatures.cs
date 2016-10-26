// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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
                "da/TestApp.resources.dll",
                "da/TestLibrary.resources.dll",
                "de/TestApp.resources.dll",
                "de/TestLibrary.resources.dll",
                "fr/TestApp.resources.dll",
                "fr/TestLibrary.resources.dll",
            });

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, "TestApp.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                // Ensure Newtonsoft.Json doesn't get excluded from the deps.json file.
                // TestLibrary has a hard dependency on Newtonsoft.Json.
                // TestApp has a PrivateAssets=All dependency on Microsoft.Extensions.DependencyModel, which depends on Newtonsoft.Json.
                // This verifies that P2P references get walked correctly when doing PrivateAssets exclusion.
                dependencyContext
                    .RuntimeLibraries
                    .FirstOrDefault(l => string.Equals(l.Name, "newtonsoft.json", StringComparison.OrdinalIgnoreCase))
                    .Should()
                    .NotBeNull();

                // Verify P2P references get created correctly in the .deps.json file.
                var testLibrary = dependencyContext
                    .RuntimeLibraries
                    .FirstOrDefault(l => string.Equals(l.Name, "testlibrary", StringComparison.OrdinalIgnoreCase));

                testLibrary.RuntimeAssemblyGroups.Count.Should().Be(1);
                testLibrary.RuntimeAssemblyGroups[0].Runtime.Should().Be(string.Empty);
                testLibrary.RuntimeAssemblyGroups[0].AssetPaths.Count.Should().Be(1);
                testLibrary.RuntimeAssemblyGroups[0].AssetPaths[0].Should().Be("TestLibrary.dll");

                testLibrary.ResourceAssemblies.Count.Should().Be(3);
                testLibrary.ResourceAssemblies[0].Locale.Should().Be("da");
                testLibrary.ResourceAssemblies[0].Path.Should().Be("da/TestLibrary.resources.dll");
                testLibrary.ResourceAssemblies[1].Locale.Should().Be("de");
                testLibrary.ResourceAssemblies[1].Path.Should().Be("de/TestLibrary.resources.dll");
                testLibrary.ResourceAssemblies[2].Locale.Should().Be("fr");
                testLibrary.ResourceAssemblies[2].Path.Should().Be("fr/TestLibrary.resources.dll");
            }
        }
    }
}
