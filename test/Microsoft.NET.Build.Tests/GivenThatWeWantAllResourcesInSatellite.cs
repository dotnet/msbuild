// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantAllResourcesInSatellite
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
        public void It_retrieves_strings_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AllResourcesInSatellite")
                .WithSource()
                .Restore();

            var buildCommand = new BuildCommand(Stage0MSBuild, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework: "netcoreapp1.0");
            outputDirectory.Should().OnlyHaveFiles(new[] {
                "AllResourcesInSatellite.dll",
                "AllResourcesInSatellite.pdb",
                "AllResourcesInSatellite.runtimeconfig.json",
                "AllResourcesInSatellite.runtimeconfig.dev.json",
                "AllResourcesInSatellite.deps.json",
                "en/AllResourcesInSatellite.resources.dll",
            });

            Command.Create(RepoInfo.DotNetHostPath, new[] { Path.Combine(outputDirectory.FullName, "AllResourcesInSatellite.dll") })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World from en satellite assembly");
        }
    }
}
