// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using LocalizableStrings = Microsoft.DotNet.Tools.Publish.LocalizableStrings;

namespace Microsoft.DotNet.Cli.Publish.Tests
{
    public class GivenDotnetPublishPublishesProjects : TestBase
    {
        [Fact]
        public void ItPublishesARunnablePortableApp()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            new PublishCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--framework netcoreapp3.0")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, "netcoreapp3.0", "publish", $"{testAppName}.dll");

            new DotnetCommand()
                .ExecuteWithCapturedOutput(outputDll)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItImplicitlyRestoresAProjectWhenPublishing()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new PublishCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--framework netcoreapp3.0")
                .Should().Pass();
        }

        [Fact]
        public void ItCanPublishAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = TestAssets.Get(
                    TestAssetKinds.DesktopTestProjects,
                    "NETFrameworkReferenceNETStandard20")
                .CreateInstance()
                .WithSourceFiles();

            string projectDirectory = Path.Combine(testInstance.Root.FullName, "MultiTFMTestApp");

            new PublishCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute("--framework netcoreapp3.0")
                .Should().Pass();
        }

        [Fact]
        public void ItDoesNotImplicitlyRestoreAProjectWhenPublishingWithTheNoRestoreOption()
        {
            var testAppName = "MSBuildTestApp";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            new PublishCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .ExecuteWithCapturedOutput("--framework netcoreapp3.0 --no-restore")
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
            var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();
            var outputDirectory = PublishApp(testAppName, rid, args);

            var outputProgram = Path.Combine(outputDirectory.FullName, $"{testAppName}{Constants.ExeSuffix}");

            new TestCommand(outputProgram)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData("--self-contained=false")]
        [InlineData("--no-self-contained")]
        public void ItPublishesFrameworkDependentWithRid(string args)
        {
            var testAppName = "MSBuildTestApp";
            var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();
            var outputDirectory = PublishApp(testAppName, rid, args);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                $"{testAppName}{Constants.ExeSuffix}",
                $"{testAppName}.dll",
                $"{testAppName}.pdb",
                $"{testAppName}.deps.json",
                $"{testAppName}.runtimeconfig.json",
            });

            var outputProgram = Path.Combine(outputDirectory.FullName, $"{testAppName}{Constants.ExeSuffix}");

            var command = new TestCommand(outputProgram);
            command.Environment[Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)"] =
                new RepoDirectoriesProvider().DotnetRoot;
            command.ExecuteWithCapturedOutput()
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

            new DotnetCommand()
                .ExecuteWithCapturedOutput(Path.Combine(outputDirectory.FullName, $"{testAppName}.dll"))
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData("--self-contained --no-self-contained")]
        [InlineData("--self-contained=true --no-self-contained")]
        public void ItFailsToPublishWithConflictingArgument(string args)
        {
            var testAppName = "MSBuildTestApp";
            var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();

            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance($"PublishApp_{rid ?? "none"}_{args ?? "none"}")
                .WithSourceFiles()
                .WithRestoreFiles();

            var testProjectDirectory = testInstance.Root;

            new PublishCommand()
                .WithRuntime(rid)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(args)
                .Should().Fail()
                    .And.HaveStdErrContaining(LocalizableStrings.SelfContainAndNoSelfContainedConflict);
        }

        private DirectoryInfo PublishApp(string testAppName, string rid, string args = null)
        {
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance($"PublishApp_{rid ?? "none"}_{args ?? "none"}")
                .WithSourceFiles()
                .WithRestoreFiles();

            var testProjectDirectory = testInstance.Root;

            new PublishCommand()
                .WithRuntime(rid)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(args ?? "")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            return testProjectDirectory
                    .GetDirectory("bin", configuration, "netcoreapp3.0", rid ?? "", "publish");
        }

        [Fact]
        public void ItPublishesAppWhenRestoringToSpecificPackageDirectory()
        {
            string dir = "pkgs";
            string args = $"--packages {dir}";

            var testInstance = TestAssets.Get("TestAppSimple")
                .CreateInstance()
                .WithSourceFiles();
            var rootDir = testInstance.Root;

            new RestoreCommand()
                .WithWorkingDirectory(rootDir)
                .Execute(args)
                .Should()
                .Pass();

            new PublishCommand()
                .WithWorkingDirectory(rootDir)
                .ExecuteWithCapturedOutput("--no-restore")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputProgram = rootDir
                .GetDirectory("bin", configuration, "netcoreapp3.0", "publish", $"{rootDir.Name}.dll")
                .FullName;

            new TestCommand(outputProgram)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItFailsToPublishWithNoBuildIfNotPreviouslyBuilt()
        {
            var testInstance = TestAssets.Get("TestAppSimple")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles(); // note implicit restore here

            var rootPath = testInstance.Root;

            new PublishCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput("--no-build")
                .Should()
                .Fail()
                .And.HaveStdOutContaining("MSB3030"); // "Could not copy ___ because it was not found."
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ItPublishesSuccessfullyWithNoBuildIfPreviouslyBuilt(bool selfContained)
        {
            var testInstance = TestAssets.Get("TestAppSimple")
                .CreateInstance(nameof(ItPublishesSuccessfullyWithNoBuildIfPreviouslyBuilt) + selfContained)
                .WithSourceFiles();

            var rootPath = testInstance.Root;

            var rid = selfContained ? DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier() : "";
            var ridArg = selfContained ? $"-r {rid}" : "";

            new BuildCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput(ridArg)
                .Should()
                .Pass();

            new PublishCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput($"{ridArg} --no-build")
                .Should()
                .Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputProgram = rootPath
                .GetDirectory("bin", configuration, "netcoreapp3.0", rid, "publish", $"{rootPath.Name}.dll")
                .FullName;

            new TestCommand(outputProgram)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItFailsToPublishWithNoBuildIfPreviouslyBuiltWithoutRid()
        {
            var testInstance = TestAssets.Get("TestAppSimple")
                .CreateInstance()
                .WithSourceFiles();

            var rootPath = testInstance.Root;

            new BuildCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass();

            new PublishCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput("-r win-x64 --no-build")
                .Should()
                .Fail();
        }

        [Fact]
        public void ItDoesNotPrintCopyrightInfo()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            var cmd = new PublishCommand()
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
