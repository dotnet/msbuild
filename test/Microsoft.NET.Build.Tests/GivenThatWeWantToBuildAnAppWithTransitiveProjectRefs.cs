// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;
using FluentAssertions;
using System.Xml.Linq;
using System.Linq;
using System;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithTransitiveProjectRefs : SdkTest
    {
        //[Fact]
        public void It_builds_the_project_successfully()
        {
            // NOTE the project dependencies in AppWithTransitiveProjectRefs:
            // TestApp --depends on--> MainLibrary --depends on--> AuxLibrary
            // (TestApp transitively depends on AuxLibrary)

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithTransitiveProjectRefs")
                .WithSource();

            testAsset.Restore("TestApp");
            testAsset.Restore("MainLibrary");
            testAsset.Restore("AuxLibrary");

            VerifyAppBuilds(testAsset);
        }

        void VerifyAppBuilds(TestAsset testAsset)
        {
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            var buildCommand = new BuildCommand(Stage0MSBuild, appProjectDirectory);
            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp1.0");

            buildCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.dll",
                "TestApp.pdb",
                "TestApp.deps.json",
                "TestApp.runtimeconfig.json",
                "TestApp.runtimeconfig.dev.json",
                "MainLibrary.dll",
                "MainLibrary.pdb",
                "AuxLibrary.dll",
                "AuxLibrary.pdb",
            });

            Command.Create(RepoInfo.DotNetHostPath, new[] { Path.Combine(outputDirectory.FullName, "TestApp.dll") })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("This string came from MainLibrary!")
                .And
                .HaveStdOutContaining("This string came from AuxLibrary!");
        }

        //[Fact]
        public void The_clean_target_removes_all_files_from_the_output_folder()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithTransitiveProjectRefs")
                .WithSource()
                .Restore("TestApp");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            var buildCommand = new BuildCommand(Stage0MSBuild, appProjectDirectory);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp1.0");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.dll",
                "TestApp.pdb",
                "TestApp.deps.json",
                "TestApp.runtimeconfig.dev.json",
                "TestApp.runtimeconfig.json",
                "MainLibrary.dll",
                "MainLibrary.pdb",
                "AuxLibrary.dll",
                "AuxLibrary.pdb"
            });

            var cleanCommand = Stage0MSBuild.CreateCommandForTarget("Clean", buildCommand.FullPathProjectFile);

            cleanCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().OnlyHaveFiles(Array.Empty<string>());
        }
    }
}
