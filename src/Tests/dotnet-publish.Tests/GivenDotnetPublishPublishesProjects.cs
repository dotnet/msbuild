// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using LocalizableStrings = Microsoft.DotNet.Tools.Publish.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Publish.Tests
{
    public class GivenDotnetPublishPublishesProjects : SdkTest
    {
        public GivenDotnetPublishPublishesProjects(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItPublishesARunnablePortableApp()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new RestoreCommand(testInstance)
                .Execute()
                .Should().Pass();

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--framework", "netcoreapp3.1")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, "netcoreapp3.1", "publish", $"{testAppName}.dll");

            new DotnetCommand(Log)
                .Execute(outputDll)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenPublishing()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--framework", "netcoreapp3.1")
                .Should().Pass();
        }

        [Fact]
        public void ItCanPublishAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = _testAssetsManager.CopyTestAsset(
                    "NETFrameworkReferenceNETStandard20",
                    testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
                .WithSource();

            string projectDirectory = Path.Combine(testInstance.Path, "MultiTFMTestApp");

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("--framework", "netcoreapp3.1")
                .Should().Pass();
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenPublishingWithTheNoRestoreOption()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName)
                            .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--framework", "netcoreapp3.0", "--no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("--self-contained")]
        [InlineData("--self-contained=true")]
        public void ItPublishesSelfContainedWithRid(string args)
        {
            var testAppName = "MSBuildTestApp";
            var rid = EnvironmentInfo.GetCompatibleRid();
            var outputDirectory = PublishApp(testAppName, rid, args);

            var outputProgram = Path.Combine(outputDirectory.FullName, $"{testAppName}{Constants.ExeSuffix}");

            new RunExeCommand(Log, outputProgram)
                .Execute()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData("--self-contained=false")]
        [InlineData("--no-self-contained")]
        public void ItPublishesFrameworkDependentWithRid(string args)
        {
            var testAppName = "MSBuildTestApp";
            var rid = EnvironmentInfo.GetCompatibleRid();
            var outputDirectory = PublishApp(testAppName, rid, args);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testAppName}{Constants.ExeSuffix}",
                $"{testAppName}.dll",
                $"{testAppName}.pdb",
                $"{testAppName}.deps.json",
                $"{testAppName}.runtimeconfig.json",
            });

            var outputProgram = Path.Combine(outputDirectory.FullName, $"{testAppName}{Constants.ExeSuffix}");

            var command = new RunExeCommand(Log, outputProgram);
            command.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData("--self-contained=false")]
        [InlineData(null)]
        [InlineData("--no-self-contained")]
        public void ItPublishesFrameworkDependentWithoutRid(string args)
        {
            var testAppName = "MSBuildTestApp";
            var outputDirectory = PublishApp(testAppName, rid: null, args: args);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testAppName}{Constants.ExeSuffix}",
                $"{testAppName}.dll",
                $"{testAppName}.pdb",
                $"{testAppName}.deps.json",
                $"{testAppName}.runtimeconfig.json",
            });

            new DotnetCommand(Log)
                .Execute(Path.Combine(outputDirectory.FullName, $"{testAppName}.dll"))
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData("--self-contained --no-self-contained")]
        [InlineData("--self-contained=true --no-self-contained")]
        public void ItFailsToPublishWithConflictingArgument(string args)
        {
            var testAppName = "MSBuildTestApp";
            var rid = EnvironmentInfo.GetCompatibleRid();

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, identifier: args)
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithRuntime(rid)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(args.Split())
                .Should().Fail()
                    .And.HaveStdErrContaining(LocalizableStrings.SelfContainAndNoSelfContainedConflict);
        }

        private DirectoryInfo PublishApp(string testAppName, string rid, string args = null, [CallerMemberName] string callingMethod = "")
        {
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, callingMethod: callingMethod, identifier: $"{rid ?? "none"}_{args ?? "none"}")
                .WithSource();

            var testProjectDirectory = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithRuntime(rid)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(args?.Split() ?? Array.Empty<string>())
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            return new DirectoryInfo(Path.Combine(testProjectDirectory, "bin", configuration, "netcoreapp3.1", rid ?? "", "publish"));
        }

        [Fact]
        public void ItPublishesAppWhenRestoringToSpecificPackageDirectory()
        {
            string dir = "pkgs";
            string args = $"--packages {dir}";

            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource()
                .Restore(Log);

            var rootDir = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(rootDir)
                .Execute("--no-restore")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputProgram = Path.Combine(rootDir, "bin", configuration, "netcoreapp3.1", "publish", $"TestAppSimple.dll");

            new DotnetCommand(Log, outputProgram)
                .Execute()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItFailsToPublishWithNoBuildIfNotPreviouslyBuilt()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource()
                .Restore(Log);

            var rootPath = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("--no-build")
                .Should()
                .Fail()
                .And.HaveStdOutContaining("MSB3030"); // "Could not copy ___ because it was not found."
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ItPublishesSuccessfullyWithNoBuildIfPreviouslyBuilt(bool selfContained)
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple", identifier: selfContained.ToString())
                .WithSource();

            var rootPath = testInstance.Path;

            var rid = selfContained ? EnvironmentInfo.GetCompatibleRid() : "";
            var ridArgs = selfContained ? $"-r {rid}".Split() : Array.Empty<string>();

            new DotnetBuildCommand(Log, rootPath)
                .Execute(ridArgs)
                .Should()
                .Pass();

            new DotnetPublishCommand(Log, "--no-build")
                .WithWorkingDirectory(rootPath)
                .Execute(ridArgs)
                .Should()
                .Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputProgram = Path.Combine(rootPath, "bin", configuration, "netcoreapp3.1", rid, "publish", $"TestAppSimple.dll");

            new DotnetCommand(Log, outputProgram)
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItFailsToPublishWithNoBuildIfPreviouslyBuiltWithoutRid()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource();

            var rootPath = testInstance.Path;

            new BuildCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(rootPath)
                .Execute("-r", "win-x64", "--no-build")
                .Should()
                .Fail();
        }

        [Fact]
        public void DotnetPublishDoesNotPrintCopyrightInfo()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("MSBuildTestApp")
                .WithSource();

            var cmd = new DotnetPublishCommand(Log)
               .WithWorkingDirectory(testInstance.Path)
               .Execute("--nologo");

            cmd.Should().Pass();

            if (!TestContext.IsLocalized())
            {
                cmd.Should().NotHaveStdOutContaining("Copyright (C) Microsoft Corporation. All rights reserved.");
            }
        }

        [Fact]
        public void DotnetPublishAllowsPublishOutputDir()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("TestAppSimple")
                .WithSource()
                .Restore(Log);

            var rootDir = testInstance.Path;

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(rootDir)
                .Execute("--no-restore", "-o", "publish")
                .Should()
                .Pass();
        }
    }
}
