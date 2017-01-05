// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Xunit;
using static Microsoft.NET.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAppsWithFrameworkRefs : SdkTest
    {
        //[Fact]
        public void It_builds_the_projects_successfully()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences")
                .WithSource();

            testAsset.Restore("EntityFrameworkApp");
            testAsset.Restore("StopwatchLib");

            VerifyProjectsBuild(testAsset);
        }

        //[Fact]
        public void It_builds_with_disable_implicit_frameworkRefs()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences")
                .WithSource();

            testAsset.Restore("EntityFrameworkApp");
            testAsset.Restore("StopwatchLib");

            VerifyProjectsBuild(testAsset, "/p:DisableImplicitFrameworkReferences=true");
        }

        void VerifyProjectsBuild(TestAsset testAsset, params string[] buildArgs)
        {
            VerifyBuild(testAsset, "StopwatchLib", "net45", buildArgs,
                "StopwatchLib.dll",
                "StopwatchLib.pdb");

            VerifyBuild(testAsset, "EntityFrameworkApp", "net451", buildArgs,
                "EntityFrameworkApp.exe",
                "EntityFrameworkApp.pdb",
                "EntityFrameworkApp.runtimeconfig.dev.json",
                "EntityFrameworkApp.runtimeconfig.json");

            // Try running EntityFrameworkApp.exe
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "EntityFrameworkApp");
            var buildCommand = new BuildCommand(Stage0MSBuild, appProjectDirectory);
            var outputDirectory = buildCommand.GetOutputDirectory("net451");

            Command.Create(Path.Combine(outputDirectory.FullName, "EntityFrameworkApp.exe"), Enumerable.Empty<string>())
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Required Test Provider");
        }

        private void VerifyBuild(TestAsset testAsset, string project, string targetFramework, 
            string [] buildArgs,
            params string [] expectedFiles)
        {
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, project);

            var buildCommand = new BuildCommand(Stage0MSBuild, appProjectDirectory);
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            buildCommand
                .Execute(buildArgs)
                .Should()
                .Pass();

            outputDirectory.Should().HaveFiles(expectedFiles);
        }

        //[Fact]
        public void The_clean_target_removes_all_files_from_the_output_folder()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences")
                .WithSource();

            testAsset.Restore("EntityFrameworkApp");
            testAsset.Restore("StopwatchLib");

            VerifyClean(testAsset, "StopwatchLib", "net45",
                "StopwatchLib.dll",
                "StopwatchLib.pdb");

            VerifyClean(testAsset, "EntityFrameworkApp", "net451",
                "EntityFrameworkApp.exe",
                "EntityFrameworkApp.pdb",
                "EntityFrameworkApp.runtimeconfig.dev.json",
                "EntityFrameworkApp.runtimeconfig.json");
        }

        private void VerifyClean(TestAsset testAsset, string project, string targetFramework,
            params string[] expectedFiles)
        {
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, project);

            var buildCommand = new BuildCommand(Stage0MSBuild, appProjectDirectory);
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().HaveFiles(expectedFiles);

            var cleanCommand = Stage0MSBuild.CreateCommandForTarget("Clean", buildCommand.FullPathProjectFile);

            cleanCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().OnlyHaveFiles(Array.Empty<string>());
        }
    }
}
