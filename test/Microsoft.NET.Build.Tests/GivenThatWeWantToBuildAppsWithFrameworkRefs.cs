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
    public class GivenThatWeWantToBuildAppsWithFrameworkRefs
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
        public void It_builds_the_projects_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences")
                .WithSource();

            testAsset.Restore("EntityFrameworkApp");
            testAsset.Restore("StopwatchLib");

            VerifyProjectsBuild(testAsset);
        }

        [Fact]
        public void It_builds_with_disable_implicit_frameworkRefs()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    // Some framework references may be provided implicitly.
                    // Turn off this implicit addition to verify that references are
                    // provided in this scenario by project assets file

                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").FirstOrDefault();
                    propertyGroup.Should().NotBeNull();

                    propertyGroup.Add(new XElement(ns + "DisableImplicitFrameworkReferences", "true"));
                });

            testAsset.Restore("EntityFrameworkApp");
            testAsset.Restore("StopwatchLib");

            VerifyProjectsBuild(testAsset);
        }

        [Fact]
        public void It_builds_the_projects_successfully_twice()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppsWithFrameworkReferences")
                .WithSource();

            testAsset.Restore("EntityFrameworkApp");
            testAsset.Restore("StopwatchLib");

            for (int i = 0; i < 2; i++)
            {
                VerifyProjectsBuild(testAsset);
            }
        }

        void VerifyProjectsBuild(TestAsset testAsset)
        {
            VerifyBuild(testAsset, "StopwatchLib", "net45",
                "StopwatchLib.dll",
                "StopwatchLib.pdb");

            VerifyBuild(testAsset, "EntityFrameworkApp", "net451",
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
            params string [] expectedFiles)
        {
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, project);

            var buildCommand = new BuildCommand(Stage0MSBuild, appProjectDirectory);
            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().HaveFiles(expectedFiles);
        }

        [Fact]
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
