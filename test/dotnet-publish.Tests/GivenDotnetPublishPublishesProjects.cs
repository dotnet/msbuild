// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
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
        public void ItPublishesARunnableSelfContainedApp()
        {
            var testAppName = "MSBuildTestApp";

            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithSourceFiles()
                .WithRestoreFiles();

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

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //Workaround for https://github.com/dotnet/corefx/issues/15516
                Process.Start("chmod", $"u+x {outputProgram}").WaitForExit();
            }

            new TestCommand(outputProgram)
                .ExecuteWithCapturedOutput()
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ItPublishesAnAppExplicitlySpecifyingSelfContained(bool selfContained)
        {
            var testAppName = "MSBuildTestApp";

            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance($"PublishesSelfContained{selfContained}")
                .WithSourceFiles()
                .WithRestoreFiles();

            var testProjectDirectory = testInstance.Root;

            var rid = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();

            new PublishCommand()
                .WithRuntime(rid)
                .WithSelfContained(selfContained)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputPath = testProjectDirectory
                    .GetDirectory("bin", configuration, "netcoreapp2.0", rid, "publish")
                    .FullName;
            var selfContainedProgram = Path.Combine(outputPath, $"{testAppName}{Constants.ExeSuffix}");
            var selfContainedProgramFile = new FileInfo(selfContainedProgram);

            TestCommand testCommand;
            string testArgs;
            if (selfContained)
            {
                selfContainedProgramFile.Should().Exist();

                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    //Workaround for https://github.com/dotnet/corefx/issues/15516
                    Process.Start("chmod", $"u+x {selfContainedProgram}").WaitForExit();
                }

                testCommand = new TestCommand(selfContainedProgram);
                testArgs = null;
            }
            else
            {
                selfContainedProgramFile.Should().NotExist();

                testCommand = new DotnetCommand();
                testArgs = Path.Combine(outputPath, $"{testAppName}.dll");
            }

            testCommand
                .ExecuteWithCapturedOutput(testArgs)
                .Should().Pass()
                     .And.HaveStdOutContaining("Hello World");
        }

        [Fact]
        public void ItPublishesAppWhenRestoringToSpecificPackageDirectory()
        {
            var rootPath = TestAssets.CreateTestDirectory().FullName;
            var rootDir = new DirectoryInfo(rootPath);

            string dir = "pkgs";
            string args = $"--packages {dir}";

            string newArgs = $"console -o \"{rootPath}\"";
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
                .ExecuteWithCapturedOutput()
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
