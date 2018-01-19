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
using FluentAssertions;
using System.Xml.Linq;
using System.Linq;
using System;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithLibrary : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_the_project_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource();

            testAsset.Restore(Log, "TestApp");
            testAsset.Restore(Log, "TestLibrary");

            VerifyAppBuilds(testAsset);
        }

        [Fact]
        public void It_builds_the_project_successfully_twice()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource();

            testAsset.Restore(Log, "TestApp");
            testAsset.Restore(Log, "TestLibrary");


            for (int i = 0; i < 2; i++)
            {
                VerifyAppBuilds(testAsset);
            }
        }

        void VerifyAppBuilds(TestAsset testAsset)
        {
            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            var buildCommand = new BuildCommand(Log, appProjectDirectory);
            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp1.1");

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
                "TestLibrary.dll",
                "TestLibrary.pdb",
            });

            Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { Path.Combine(outputDirectory.FullName, "TestApp.dll") })
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("This string came from the test library!");

            var appInfo = FileVersionInfo.GetVersionInfo(Path.Combine(outputDirectory.FullName, "TestApp.dll"));
            appInfo.CompanyName.Should().Be("Test Authors");
            appInfo.FileVersion.Should().Be("1.2.3.0");
            appInfo.FileDescription.Should().Be("Test AssemblyTitle");
            appInfo.LegalCopyright.Should().Be("Copyright (c) Test Authors");
            appInfo.ProductName.Should().Be("Test Product");
            appInfo.ProductVersion.Should().Be("1.2.3-beta");

            var libInfo = FileVersionInfo.GetVersionInfo(Path.Combine(outputDirectory.FullName, "TestLibrary.dll"));
            libInfo.CompanyName.Trim().Should().Be("TestLibrary");
            libInfo.FileVersion.Should().Be("42.43.44.45");
            libInfo.FileDescription.Should().Be("TestLibrary");
            libInfo.LegalCopyright.Trim().Should().BeEmpty();
            libInfo.ProductName.Should().Be("TestLibrary");
            libInfo.ProductVersion.Should().Be("42.43.44.45-alpha");
        }

        [Fact]
        public void It_generates_satellite_assemblies()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("KitchenSink")
                .WithSource();

            testAsset.Restore(Log, "TestApp");
            testAsset.Restore(Log, "TestLibrary");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            var buildCommand = new BuildCommand(Log, appProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDir = buildCommand.GetOutputDirectory("netcoreapp2.0");

            var commandResult = Command.Create(TestContext.Current.ToolsetUnderTest.DotNetHostPath, new[] { Path.Combine(outputDir.FullName, "TestApp.dll") })
                .CaptureStdOut()
                .Execute();

            commandResult.Should().Pass();

            Dictionary<string, string> cultureValueMap = new Dictionary<string, string>()
            {
                {"", "Welcome to .Net!"},
                {"da", "Velkommen til .Net!"},
                {"de", "Willkommen in .Net!"},
                {"fr", "Bienvenue à .Net!"}
            };

            foreach (var cultureValuePair in cultureValueMap)
            {
                var culture = cultureValuePair.Key;
                var val = cultureValuePair.Value;

                if (culture != "")
                {
                    var cultureDir = new DirectoryInfo(Path.Combine(outputDir.FullName, culture));
                    cultureDir.Should().Exist();
                    cultureDir.Should().HaveFile("TestApp.resources.dll");
                    cultureDir.Should().HaveFile("TestLibrary.resources.dll");
                }

                commandResult.Should().HaveStdOutContaining(val);
            }
        }

        [WindowsOnlyFact]
        public void The_clean_target_removes_all_files_from_the_output_folder()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource()
                .Restore(Log, "TestApp");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            var buildCommand = new BuildCommand(Log, appProjectDirectory);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory("netcoreapp1.1");

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.dll",
                "TestApp.pdb",
                "TestApp.deps.json",
                "TestApp.runtimeconfig.dev.json",
                "TestApp.runtimeconfig.json",
                "TestLibrary.dll",
                "TestLibrary.pdb"
            });

            var cleanCommand = new MSBuildCommand(Log, "Clean", buildCommand.FullPathProjectFile);

            cleanCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().OnlyHaveFiles(Array.Empty<string>());
        }

        [Fact]
        public void An_appx_app_can_reference_a_cross_targeted_library()
        {
            var asset = _testAssetsManager
                .CopyTestAsset("AppxReferencingCrossTargeting")
                .WithSource()
                .Restore(Log, "Appx");

            var buildCommand = new BuildCommand(Log, Path.Combine(asset.TestRoot, "Appx"));

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
