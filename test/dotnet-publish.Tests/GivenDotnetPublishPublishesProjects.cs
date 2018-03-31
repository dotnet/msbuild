// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

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
                .Execute("--framework netcoreapp2.0")
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, "netcoreapp2.0", "publish", $"{testAppName}.dll");

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
                .Execute("--framework netcoreapp2.0")
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
                .Execute("--framework netcoreapp2.0")
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
                .ExecuteWithCapturedOutput("--framework netcoreapp2.0 --no-restore")
                .Should().Fail()
                .And.HaveStdOutContaining("project.assets.json");
        }

        [Fact]
        public void ItPublishesARunnableSelfContainedApp()
        {
            var testAppName = "MSBuildTestApp";

            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithSourceFiles()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();

                    // This is needed to be able to restore for RIDs that were not available in Microsoft.NetCore.App 2.0.0.
                    // M.NC.App 2.0.0 depends on a version of Microsoft.NetCore.Platforms that lacks the mapping for the
                    // latest RIDs. Given that self-contained apps are pinned to 2.0.0 in this version of the SDK, we
                    // need a manual roll-forward.
                    propertyGroup.Add(
                        new XElement(ns + "RuntimeFrameworkVersion", "2.0.*"));
                });

            var testProjectDirectory = testInstance.Root;

            var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();

            new PublishCommand()
                .WithFramework("netcoreapp2.0")
                .WithRuntime(rid)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputProgram = testProjectDirectory
                .GetDirectory("bin", configuration, "netcoreapp2.0", rid, "publish", $"{testAppName}{Constants.ExeSuffix}")
                .FullName;

            EnsureProgramIsRunnable(outputProgram);

            new TestCommand(outputProgram)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItPublishesARidSpecificAppSettingSelfContainedToTrue()
        {
            var testAppName = "MSBuildTestApp";
            var outputDirectory = PublishAppWithSelfContained(testAppName, true);

            var outputProgram = Path.Combine(outputDirectory.FullName, $"{testAppName}{Constants.ExeSuffix}");

            EnsureProgramIsRunnable(outputProgram);

            new TestCommand(outputProgram)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItPublishesARidSpecificAppSettingSelfContainedToFalse()
        {
            var testAppName = "MSBuildTestApp";
            var outputDirectory = PublishAppWithSelfContained(testAppName, false);

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

        private DirectoryInfo PublishAppWithSelfContained(string testAppName, bool selfContained)
        {
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance($"PublishesSelfContained{selfContained}")
                .WithSourceFiles()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();

                    // This is needed to be able to restore for RIDs that were not available in Microsoft.NetCore.App 2.0.0.
                    // M.NC.App 2.0.0 depends on a version of Microsoft.NetCore.Platforms that lacks the mapping for the
                    // latest RIDs. Given that self-contained apps are pinned to 2.0.0 in this version of the SDK, we
                    // need a manual roll-forward.
                    propertyGroup.Add(
                        new XElement(ns + "RuntimeFrameworkVersion", "2.0.*"));
                });

            var testProjectDirectory = testInstance.Root;

            var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();

            new PublishCommand()
                .WithRuntime(rid)
                .WithSelfContained(selfContained)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            return testProjectDirectory
                    .GetDirectory("bin", configuration, "netcoreapp2.0", rid, "publish");
        }

        private static void EnsureProgramIsRunnable(string path)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //Workaround for https://github.com/dotnet/corefx/issues/15516
                Process.Start("chmod", $"u+x {path}").WaitForExit();
            }
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
                .GetDirectory("bin", configuration, "netcoreapp2.0", "publish", $"{rootDir.Name}.dll")
                .FullName;

            new TestCommand(outputProgram)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }
    }
}
