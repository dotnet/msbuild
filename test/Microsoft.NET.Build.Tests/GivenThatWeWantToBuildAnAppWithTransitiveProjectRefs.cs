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
    public class GivenThatWeWantToBuildAnAppWithTransitiveProjectRefs
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
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

        [Fact]
        public void It_builds_the_project_successfully_twice()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithTransitiveProjectRefs")
                .WithSource();

            testAsset.Restore("TestApp");
            testAsset.Restore("MainLibrary");
            testAsset.Restore("AuxLibrary");

            for (int i = 0; i < 2; i++)
            {
                VerifyAppBuilds(testAsset);
            }
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

            var appInfo = FileVersionInfo.GetVersionInfo(Path.Combine(outputDirectory.FullName, "TestApp.dll"));
            appInfo.CompanyName.Should().Be("Test Authors");
            appInfo.FileVersion.Should().Be("1.2.3.0");
            appInfo.FileDescription.Should().Be("Test AssemblyTitle");
            appInfo.LegalCopyright.Should().Be("Copyright (c) Test Authors");
            appInfo.ProductName.Should().Be("Test Product");

            // This check is blocked from working on non-Windows by https://github.com/dotnet/corefx/issues/11163
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                appInfo.ProductVersion.Should().Be("1.2.3-beta");
            }

            new List<string> { "MainLibrary", "AuxLibrary" }.ForEach(libName => 
            {
                var libInfo = FileVersionInfo.GetVersionInfo(Path.Combine(outputDirectory.FullName, $"{libName}.dll"));
                libInfo.CompanyName.Trim().Should().Be(libName);
                libInfo.FileVersion.Should().Be("42.43.44.45");
                libInfo.FileDescription.Should().Be(libName);
                libInfo.LegalCopyright.Trim().Should().BeEmpty();
                libInfo.ProductName.Should().Be(libName);

                // This check is blocked from working on non-Windows by https://github.com/dotnet/corefx/issues/11163
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    libInfo.ProductVersion.Should().Be("42.43.44.45-alpha");
                }
            });
        }

        //[Fact]
        public void The_clean_target_removes_all_files_from_the_output_folder()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
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
                "TestLibrary.dll",
                "TestLibrary.pdb"
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
