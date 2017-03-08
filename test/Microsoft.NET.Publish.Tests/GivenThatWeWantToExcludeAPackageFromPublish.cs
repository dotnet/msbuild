// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using System.Xml.Linq;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToExcludeAPackageFromPublish : SdkTest
    {
        [Fact]
        public void It_does_not_publish_a_PackageReference_with_PrivateAssets_All()
        {
            var helloWorldAsset = _testAssetsManager
                .CopyTestAsset("HelloWorld", "PublishExcludePackage")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    //  Using different casing for the package ID here, to test the scenario from https://github.com/dotnet/sdk/issues/376
                    itemGroup.Add(new XElement(ns + "PackageReference", new XAttribute("Include", "NEWTONSOFT.Json"),
                                                                        new XAttribute("Version", "9.0.1"),
                                                                        new XAttribute("PrivateAssets", "All")));
                })
                .Restore();

            var publishCommand = new PublishCommand(Stage0MSBuild, helloWorldAsset.TestRoot);
            var publishResult = publishCommand.Execute();

            publishResult.Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().OnlyHaveFiles(new[] {
                "HelloWorld.dll",
                "HelloWorld.pdb",
                "HelloWorld.deps.json",
                "HelloWorld.runtimeconfig.json"
            });
        }
    }
}
