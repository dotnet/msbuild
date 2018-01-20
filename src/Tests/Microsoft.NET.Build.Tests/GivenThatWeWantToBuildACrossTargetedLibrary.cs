// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using System.Xml.Linq;
using System.Linq;
using FluentAssertions;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildACrossTargetedLibrary : SdkTest
    {
        public GivenThatWeWantToBuildACrossTargetedLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_nondesktop_library_successfully_on_all_platforms()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CrossTargeting")
                .WithSource()
                .Restore(Log, "NetStandardAndNetCoreApp");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "NetStandardAndNetCoreApp");

            var buildCommand = new BuildCommand(Log, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: "");
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "netcoreapp1.1/NetStandardAndNetCoreApp.dll",
                "netcoreapp1.1/NetStandardAndNetCoreApp.pdb",
                "netcoreapp1.1/NetStandardAndNetCoreApp.runtimeconfig.json",
                "netcoreapp1.1/NetStandardAndNetCoreApp.runtimeconfig.dev.json",
                "netcoreapp1.1/NetStandardAndNetCoreApp.deps.json",
                "netstandard1.5/NetStandardAndNetCoreApp.dll",
                "netstandard1.5/NetStandardAndNetCoreApp.pdb",
                "netstandard1.5/NetStandardAndNetCoreApp.deps.json"
            });
        }

        [WindowsOnlyFact]
        public void It_builds_desktop_library_successfully_on_windows()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CrossTargeting")
                .WithSource()
                .Restore(Log, "DesktopAndNetStandard");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "DesktopAndNetStandard");

            var buildCommand = new BuildCommand(Log, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: "");
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "net40/DesktopAndNetStandard.dll",
                "net40/DesktopAndNetStandard.pdb",
                "net40/Newtonsoft.Json.dll",
                "net40-client/DesktopAndNetStandard.dll",
                "net40-client/DesktopAndNetStandard.pdb",
                "net40-client/Newtonsoft.Json.dll",
                "net45/DesktopAndNetStandard.dll",
                "net45/DesktopAndNetStandard.pdb",
                "net45/Newtonsoft.Json.dll",
                "netstandard1.5/DesktopAndNetStandard.dll",
                "netstandard1.5/DesktopAndNetStandard.pdb",
                "netstandard1.5/DesktopAndNetStandard.deps.json"
            });
        }

        [Theory]
        [InlineData("1", "win7-x86", "win7-x86;win7-x64", "win10-arm", "win7-x86;linux;WIN7-X86;unix", "osx-10.12", "win8-arm;win8-arm-aot",
            "win7-x86;win7-x64;win10-arm;linux;unix;osx-10.12;win8-arm;win8-arm-aot")]
        public void It_combines_inner_rids_for_restore(
            string identifier,
            string outerRid,
            string outerRids,
            string firstFrameworkRid,
            string firstFrameworkRids,
            string secondFrameworkRid,
            string secondFrameworkRids,
            string expectedCombination)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset(Path.Combine("CrossTargeting", "NetStandardAndNetCoreApp"), identifier: identifier)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();

                    propertyGroup.Add(
                        new XElement(ns + "RuntimeIdentifier", outerRid),
                        new XElement(ns + "RuntimeIdentifiers", outerRids));

                    propertyGroup.AddAfterSelf(
                        new XElement(ns + "PropertyGroup",
                            new XAttribute(ns + "Condition", "'$(TargetFramework)' == 'netstandard1.5'"),
                            new XElement(ns + "RuntimeIdentifier", firstFrameworkRid),
                            new XElement(ns + "RuntimeIdentifiers", firstFrameworkRids)),
                        new XElement(ns + "PropertyGroup",
                            new XAttribute(ns + "Condition", "'$(TargetFramework)' == 'netcoreapp1.1'"),
                            new XElement(ns + "RuntimeIdentifier", secondFrameworkRid),
                            new XElement(ns + "RuntimeIdentifiers", secondFrameworkRids)));
                });

            var command = new GetValuesCommand(Log, testAsset.TestRoot, "", valueName: "RuntimeIdentifiers");
            command.DependsOnTargets = "GetAllRuntimeIdentifiers";
            command.Execute().Should().Pass();
            command.GetValues().Should().BeEquivalentTo(expectedCombination.Split(';'));
        }
    }
}
