// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Xunit;
using System.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using Microsoft.DotNet.Tools;

namespace Microsoft.DotNet.Cli.Build.Tests
{
    public class GivenDotnetBuildBuildsCsproj : SdkTest
    {
        public GivenDotnetBuildBuildsCsproj(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItBuildsARunnableOutput()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            var buildCommand = new DotnetBuildCommand(Log, testInstance.Path);

            buildCommand
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDll = Path.Combine(testInstance.Path, "bin", configuration, "netcoreapp3.1", $"{testAppName}.dll");

            var outputRunCommand = new DotnetCommand(Log);

            outputRunCommand.Execute(outputDll)
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItBuildsOnlyTheSpecifiedTarget()
        {
            var testAppName = "NonDefaultTarget";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetBuildCommand(Log, testInstance.Path)
                .Execute("--no-restore", "--nologo", "/t:PrintMessage")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenBuilding()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetBuildCommand(Log, testInstance.Path)
                .Execute()
                .Should().Pass();
        }

        [Fact]
        public void ItCanBuildAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = _testAssetsManager.CopyTestAsset(
                    "NETFrameworkReferenceNETStandard20",
                    testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
                .WithSource();

            string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

            new DotnetBuildCommand(Log, projectDirectory)
                .Execute("--framework", "netcoreapp3.1")
                .Should().Pass();
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenBuildingWithTheNoRestoreOption()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource();

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("--no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void ItRunsWhenRestoringToSpecificPackageDir()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();
            var rootPath = testInstance.Path;

            string dir = "pkgs";

            new DotnetRestoreCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--packages", dir)
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--no-restore")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDll = Directory.EnumerateFiles(
                Path.Combine(rootPath, "bin", configuration, "netcoreapp3.1"), "*.dll",
                SearchOption.TopDirectoryOnly)
                .Single();

            var outputRunCommand = new DotnetCommand(Log);

            outputRunCommand.Execute(outputDll)
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItPrintsBuildSummary()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .Restore(Log);

            string expectedBuildSummary = @"Build succeeded.
    0 Warning(s)
    0 Error(s)";

            var cmd = new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute();
            cmd.Should().Pass();
            cmd.StdOut.Should().ContainVisuallySameFragmentIfNotLocalized(expectedBuildSummary);
        }

        [Fact]
        public void DotnetBuildDoesNotPrintCopyrightInfo()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                .WithSource()
                .Restore(Log);

            var cmd = new DotnetBuildCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute("--nologo");

            cmd.Should().Pass();

            if (!TestContext.IsLocalized())
            {
                cmd.Should().NotHaveStdOutContaining("Copyright (C) Microsoft Corporation. All rights reserved.");
            }
        }

        [Fact]
        public void It_warns_on_rid_without_self_contained_options()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFrameworkOrFrameworks("net6.0", false)
                .Restore(Log);

            new DotnetBuildCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute("-r", "win-x64")
               .Should()
               .Pass()
               .And
               .HaveStdOutContaining("NETSDK1179");
        }

        [Fact]
        public void It_does_not_warn_on_rid_with_self_contained_options()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld")
                .WithSource()
                .WithTargetFrameworkOrFrameworks("net6.0", false)
                .Restore(Log);

            new DotnetBuildCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute("-r", "win-x64", "--self-contained")
               .Should()
               .Pass()
               .And
               .NotHaveStdOutContaining("NETSDK1179");
        }

        [Fact]
        public void It_does_not_warn_on_rid_with_self_contained_options_prior_to_net6()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("HelloWorld")
                .WithSource()
                .Restore(Log);

            new DotnetBuildCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute("-r", "win-x64")
               .Should()
               .Pass()
               .And
               .NotHaveStdOutContaining("NETSDK1179");
        }
    }
}
