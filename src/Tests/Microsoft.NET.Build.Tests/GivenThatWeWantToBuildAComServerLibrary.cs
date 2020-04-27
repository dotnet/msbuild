// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAComServerLibrary : SdkTest
    {
        public GivenThatWeWantToBuildAComServerLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_copies_the_comhost_to_the_output_directory()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource();

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp3.0");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "ComServer.dll",
                "ComServer.pdb",
                "ComServer.deps.json",
                "ComServer.comhost.dll",
                "ComServer.runtimeconfig.json",
                "ComServer.runtimeconfig.dev.json"
            });

            string runtimeConfigFile = Path.Combine(outputDirectory.FullName, "ComServer.runtimeconfig.json");
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
            JObject runtimeConfig = JObject.Parse(runtimeConfigContents);
            runtimeConfig["runtimeOptions"]["rollForward"].Value<string>()
                .Should().Be("LatestMinor");
        }

        [WindowsOnlyFact]
        public void It_generates_a_regfree_com_manifest_when_requested()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("EnableRegFreeCom", true));
                });

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp3.0");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "ComServer.dll",
                "ComServer.pdb",
                "ComServer.deps.json",
                "ComServer.comhost.dll",
                "ComServer.X.manifest",
                "ComServer.runtimeconfig.json",
                "ComServer.runtimeconfig.dev.json"
            });
        }

        [WindowsOnlyTheory]
        [InlineData("win-x64")]
        [InlineData("win-x86")]
        public void It_embeds_the_clsidmap_in_the_comhost_when_rid_specified(string rid)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("RuntimeIdentifier", rid));
                });

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp3.0", runtimeIdentifier: rid);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "ComServer.dll",
                "ComServer.pdb",
                "ComServer.deps.json",
                "ComServer.comhost.dll",
                "ComServer.runtimeconfig.json",
                "ComServer.runtimeconfig.dev.json"
            });
        }

        [WindowsOnlyFact]
        public void It_warns_on_self_contained_build()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("SelfContained", true));
                });

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1128: ");
        }

        [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD)]
        public void It_fails_to_find_comhost_for_platforms_without_comhost()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                });

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1091: ");
        }

        [PlatformSpecificTheory(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD)]
        [InlineData("win-x64")]
        [InlineData("win-x86")]
        public void It_fails_to_embed_clsid_when_not_on_windows(string rid)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("ComServer")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Add(new XElement("RuntimeIdentifier", rid));
                });

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1092: ");
        }
    }
}
