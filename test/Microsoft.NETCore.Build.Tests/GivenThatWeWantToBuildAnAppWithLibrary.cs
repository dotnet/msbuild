// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NETCore.TestFramework;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Xunit;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;
using FluentAssertions;

namespace Microsoft.NETCore.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithLibrary
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
        public void It_builds_the_project_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource();

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            testAsset.Restore(appProjectDirectory, $"/p:RestoreFallbackFolders={RepoInfo.PackagesPath}");

            var buildCommand = new BuildCommand(Stage0MSBuild, appProjectDirectory);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory();

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.dll",
                "TestApp.pdb",
                "TestApp.deps.json",
                "TestApp.runtimeconfig.json",
                "TestApp.runtimeconfig.dev.json",
                "TestLibrary.dll",
                "TestLibrary.pdb",
            });

            Command.Create(RepoInfo.DotNetHostPath, new[] { Path.Combine(outputDirectory.FullName, "TestApp.dll") })
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

            // This check is blocked from working on non-Windows by https://github.com/dotnet/corefx/issues/11163
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                appInfo.ProductVersion.Should().Be("1.2.3-beta");
            }

            var libInfo = FileVersionInfo.GetVersionInfo(Path.Combine(outputDirectory.FullName, "TestLibrary.dll"));
            libInfo.CompanyName.Trim().Should().BeEmpty();
            libInfo.FileVersion.Should().Be("42.43.44.45");
            libInfo.FileDescription.Should().Be("TestLibrary");
            libInfo.LegalCopyright.Trim().Should().BeEmpty();
            libInfo.ProductName.Should().Be("TestLibrary");

            // This check is blocked from working on non-Windows by https://github.com/dotnet/corefx/issues/11163
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                libInfo.ProductVersion.Should().Be("42.43.44.45-alpha");
            }
        }
    }
}