// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Linq;

namespace Microsoft.DotNet.Cli.Build.Tests
{
    public class GivenDotnetBuildBuildsCsproj : TestBase
    {
        [Fact]
        public void ItBuildsARunnableOutput()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName)
                .WithSourceFiles()
                .WithRestoreFiles();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDll = testInstance.Root.GetDirectory("bin", configuration, "netcoreapp3.0")
                .GetFile($"{testAppName}.dll");

            var outputRunCommand = new DotnetCommand();

            outputRunCommand.ExecuteWithCapturedOutput(outputDll.FullName)
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItBuildsOnlyTheSpecifiedTarget()
        {
            var testAppName = "NonDefaultTarget";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName)
                .WithSourceFiles();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute("--no-restore --nologo /t:PrintMessage")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenBuilding()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName)
                .WithSourceFiles();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ItCanBuildAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = TestAssets.Get(
                    TestAssetKinds.DesktopTestProjects,
                    "NETFrameworkReferenceNETStandard20")
                .CreateInstance()
                .WithSourceFiles();

            string projectDirectory = Path.Combine(testInstance.Root.FullName, "MultiTFMTestApp");

            new BuildCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute("--framework netcoreapp3.0")
                .Should().Pass();
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenBuildingWithTheNoRestoreOption()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName)
                .WithSourceFiles();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("--no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void ItRunsWhenRestoringToSpecificPackageDir()
        {
            var testInstance = TestAssets.Get("TestAppSimple")
                .CreateInstance()
                .WithSourceFiles();
            var rootPath = testInstance.Root;

            string dir = "pkgs";
            string args = $"--packages {dir}";

            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new BuildCommand()
                .WithWorkingDirectory(rootPath)
                .Execute("--no-restore")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDll = Directory.EnumerateFiles(
                Path.Combine(rootPath.FullName, "bin", configuration, "netcoreapp3.0"), "*.dll",
                SearchOption.TopDirectoryOnly)
                .Single();

            var outputRunCommand = new DotnetCommand();

            outputRunCommand.ExecuteWithCapturedOutput(outputDll)
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItPrintsBuildSummary()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            string expectedBuildSummary = @"Build succeeded.
    0 Warning(s)
    0 Error(s)";

            var cmd = new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput();
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(expectedBuildSummary);
        }

        [Fact]
        public void ItDoesNotPrintCopyrightInfo()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            var cmd = new BuildCommand()
               .WithWorkingDirectory(testInstance.Root)
               .ExecuteWithCapturedOutput("--nologo");

            cmd.Should().Pass();

            if (!DotnetUnderTest.IsLocalized())
            {
                cmd.Should().NotHaveStdOutContaining("Copyright (C) Microsoft Corporation. All rights reserved.");
            }
        }
    }
}
