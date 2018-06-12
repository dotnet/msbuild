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
                .Execute("--framework netcoreapp2.1")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, "netcoreapp2.1", "publish", $"{testAppName}.dll");

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
                .Execute("--framework netcoreapp2.1")
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
                .Execute("--framework netcoreapp2.1")
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
                .ExecuteWithCapturedOutput("--framework netcoreapp2.1 --no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Theory]
        [InlineData("self-contained", null)]
        [InlineData(null, null)]
        [InlineData(null, "--self-contained")]
        [InlineData(null, "--self-contained=true")]
        public void ItPublishesSelfContainedWithRid(string mode, string args)
        {
            var testAppName = "MSBuildTestApp";
            var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();
            var outputDirectory = PublishApp(testAppName, rid, mode, args);

            var outputProgram = Path.Combine(outputDirectory.FullName, $"{testAppName}{Constants.ExeSuffix}");

            new TestCommand(outputProgram)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData("fx-dependent", null)]
        [InlineData(null, "--self-contained=false")]
        public void ItPublishesFrameworkDependentWithRid(string mode, string args)
        {
            var testAppName = "MSBuildTestApp";
            var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();
            var outputDirectory = PublishApp(testAppName, rid, mode, args);

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

        [Fact]
        public void ItPublishesFrameworkDependentNoExeWithRid()
        {
            var testAppName = "MSBuildTestApp";
            var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();
            var outputDirectory = PublishApp(testAppName, rid, mode: "fx-dependent-no-exe");

            outputDirectory.Should().OnlyHaveFiles(new[] {
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
        [InlineData("fx-dependent-no-exe", null)]
        [InlineData("fx-dependent", null)]
        [InlineData(null, "--self-contained=false")]
        [InlineData(null, null)]
        public void ItPublishesFrameworkDependentWithoutRid(string mode, string args)
        {
            var testAppName = "MSBuildTestApp";
            var outputDirectory = PublishApp(testAppName, rid: null, mode: mode, args: args);

            outputDirectory.Should().OnlyHaveFiles(new[] {
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

        private DirectoryInfo PublishApp(string testAppName, string rid, string mode, string args = null)
        {
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance($"PublishApp_{rid ?? "none"}_{mode ?? "none"}_{args ?? "none"}")
                .WithSourceFiles()
                .WithRestoreFiles();

            var testProjectDirectory = testInstance.Root;

            new PublishCommand()
                .WithRuntime(rid)
                .WithMode(mode)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(args ?? "")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            return testProjectDirectory
                    .GetDirectory("bin", configuration, "netcoreapp2.1", rid ?? "", "publish");
        }

        [Fact]
        public void ItPublishesAppWhenRestoringToSpecificPackageDirectory()
        {
            var rootPath = TestAssets.CreateTestDirectory().FullName;
            var rootDir = new DirectoryInfo(rootPath);

            string dir = "pkgs";
            string args = $"--packages {dir}";

            string newArgs = $"console -o \"{rootPath}\" --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass();

            new PublishCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput("--no-restore")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputProgram = rootDir
                .GetDirectory("bin", configuration, "netcoreapp2.1", "publish", $"{rootDir.Name}.dll")
                .FullName;

            new TestCommand(outputProgram)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItFailsToPublishWithNoBuildIfNotPreviouslyBuilt()
        {
            var rootPath = TestAssets.CreateTestDirectory().FullName;

            string newArgs = $"console -o \"{rootPath}\"";
            new NewCommandShim() // note implicit restore here
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

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
            var rootPath = TestAssets.CreateTestDirectory(identifier: selfContained ? "_sc" : "").FullName;
            var rootDir = new DirectoryInfo(rootPath);

            string newArgs = $"console -o \"{rootPath}\" --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

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

            var outputProgram = rootDir
                .GetDirectory("bin", configuration, "netcoreapp2.1", rid, "publish", $"{rootDir.Name}.dll")
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
            var rootPath = TestAssets.CreateTestDirectory().FullName;
            var rootDir = new DirectoryInfo(rootPath);

            string newArgs = $"console -o \"{rootPath}\" --no-restore";
            new NewCommandShim()
                .WithWorkingDirectory(rootPath)
                .Execute(newArgs)
                .Should()
                .Pass();

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
        public void ItFailsToPublishIfBothModeAndSelfContainedAreSpecified()
        {
            var testInstance = TestAssets.Get("MSBuildTestApp")
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

            var testProjectDirectory = testInstance.Root;

            new PublishCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute("--self-contained --mode fx-dependent")
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining(LocalizableStrings.PublishModeAndSelfContainedOptionsConflict);
        }
    }
}
