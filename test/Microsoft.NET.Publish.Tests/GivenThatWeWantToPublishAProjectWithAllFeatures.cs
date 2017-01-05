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
    public class GivenThatWeWantToPublishAProjectWithAllFeatures : SdkTest
    {
        //[Fact]
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
                "System.Spatial.dll",
                "de/System.Spatial.resources.dll",
                "es/System.Spatial.resources.dll",
                "fr/System.Spatial.resources.dll",
                "it/System.Spatial.resources.dll",
                "ja/System.Spatial.resources.dll",
                "ko/System.Spatial.resources.dll",
                "ru/System.Spatial.resources.dll",
                "zh-Hans/System.Spatial.resources.dll",
                "zh-Hant/System.Spatial.resources.dll"
            });

            using (var depsJsonFileStream = File.OpenRead(Path.Combine(publishDirectory.FullName, "TestApp.deps.json")))
            {
                var dependencyContext = new DependencyContextJsonReader().Read(depsJsonFileStream);

                // Ensure Newtonsoft.Json doesn't get excluded from the deps.json file.
                // TestLibrary has a hard dependency on Newtonsoft.Json.
                // TestApp has a PrivateAssets=All dependency on Microsoft.Extensions.DependencyModel, which depends on Newtonsoft.Json.
                // This verifies that P2P references get walked correctly when doing PrivateAssets exclusion.
                VerifyDependency(dependencyContext, "Newtonsoft.Json", "lib/netstandard1.0/");

                // Verify P2P references get created correctly in the .deps.json file.
                VerifyDependency(dependencyContext, "TestLibrary", "",
                    "da", "de", "fr");

                // Verify package reference with satellites gets created correctly in the .deps.json file
                VerifyDependency(dependencyContext, "System.Spatial", "lib/portable-net45+wp8+win8+wpa/",
                    "de", "es", "fr", "it", "ja", "ko", "ru", "zh-Hans", "zh-Hant");
            }
        }

        private static void VerifyDependency(DependencyContext dependencyContext, string name, string path, params string[] locales)
        {
            var library = dependencyContext
                .RuntimeLibraries
                .FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));

            library.Should().NotBeNull();
            library.RuntimeAssemblyGroups.Count.Should().Be(1);
            library.RuntimeAssemblyGroups[0].Runtime.Should().Be(string.Empty);
            library.RuntimeAssemblyGroups[0].AssetPaths.Count.Should().Be(1);
            library.RuntimeAssemblyGroups[0].AssetPaths[0].Should().Be($"{path}{name}.dll");

            library.ResourceAssemblies.Count.Should().Be(locales.Length);

            foreach (string locale in locales)
            {
                library
                   .ResourceAssemblies
                   .FirstOrDefault(r => r.Locale == locale && r.Path == $"{path}{locale}/{name}.resources.dll")
                   .Should()
                   .NotBeNull();
            }
        }
    }
}
