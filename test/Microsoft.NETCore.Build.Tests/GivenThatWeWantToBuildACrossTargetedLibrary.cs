// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NETCore.TestFramework;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Xunit;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NETCore.Build.Tests
{
    public class GivenThatWeWantToBuildACrossTargetedLibrary
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        //[Fact]
        public void It_builds_nondesktop_library_successfully_on_all_platforms()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("CrossTargeting")
                .WithSource()
                .Restore("NetStandardAndNetCoreApp");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "NetStandardAndNetCoreApp");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: "");
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "netcoreapp1.0/NetStandardAndNetCoreApp.dll",
                "netcoreapp1.0/NetStandardAndNetCoreApp.pdb",
                "netcoreapp1.0/NetStandardAndNetCoreApp.runtimeconfig.json",
                "netcoreapp1.0/NetStandardAndNetCoreApp.runtimeconfig.dev.json",
                "netcoreapp1.0/NetStandardAndNetCoreApp.deps.json",
                "netstandard1.5/NetStandardAndNetCoreApp.dll",
                "netstandard1.5/NetStandardAndNetCoreApp.pdb",
                "netstandard1.5/NetStandardAndNetCoreApp.deps.json"
            }, SearchOption.AllDirectories);
        }

        //[Fact]
        public void It_builds_desktop_library_successfully_on_windows()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("CrossTargeting")
                .WithSource()
                .Restore("DesktopAndNetStandard");

            var libraryProjectDirectory = Path.Combine(testAsset.TestRoot, "DesktopAndNetStandard");

            var buildCommand = new BuildCommand(Stage0MSBuild, libraryProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: "");
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "net45/DesktopAndNetStandard.dll",
                "net45/DesktopAndNetStandard.pdb",
                "net45/DesktopAndNetStandard.deps.json",
                "netstandard1.5/DesktopAndNetStandard.dll",
                "netstandard1.5/DesktopAndNetStandard.pdb",
                "netstandard1.5/DesktopAndNetStandard.deps.json"
            }, SearchOption.AllDirectories);
        }
    }
}
