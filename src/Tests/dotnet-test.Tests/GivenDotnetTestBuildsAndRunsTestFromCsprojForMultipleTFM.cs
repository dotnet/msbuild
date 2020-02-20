// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using System;
using Xunit;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestFromCsprojForMultipleTFM : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestFromCsprojForMultipleTFM(ITestOutputHelper log) : base(log)
        {
        }

        private readonly string[] ConsoleLoggerOutputNormal = new[] { "--logger", "console;verbosity=normal" };

        [WindowsOnlyFact]
        public void MStestMultiTFM()
        {
            var testProjectDirectory = _testAssetsManager.CopyTestAsset("VSTestMulti", identifier: "1")
                .WithSource()
                .WithVersionVariables()
                .Path;

            NuGetConfigWriter.Write(testProjectDirectory, TestContext.Current.TestPackages);

            var runtime = EnvironmentInfo.GetCompatibleRid();

            new DotnetRestoreCommand(Log, "-r", runtime)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should().Pass();

            var result = new DotnetTestCommand(Log, "-r", runtime)
                .WithWorkingDirectory(testProjectDirectory)
                .Execute(ConsoleLoggerOutputNormal);

            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 3")
                         .And.Contain("Passed: 2")
                         .And.Contain("Failed: 1")
                         .And.Contain("\u221a VSTestPassTestDesktop", "because .NET 4.6 tests will pass")
                         .And.Contain("Total tests: 3")
                         .And.Contain("Passed: 1")
                         .And.Contain("Failed: 2")
                         .And.Contain("X VSTestFailTestNetCoreApp", "because netcoreapp2.0 tests will fail");
            }
            result.ExitCode.Should().Be(1);
        }

        [WindowsOnlyFact]
        public void XunitMultiTFM()
        {
            // Copy XunitMulti project in output directory of project dotnet-test.Tests
            string testAppName = "XunitMulti";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, identifier: "2")
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            // Restore project XunitMulti
            new RestoreCommand(Log, testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute(ConsoleLoggerOutputNormal);

            // Verify
            if (!TestContext.IsLocalized())
            {
                // for target framework net46
                result.StdOut.Should().Contain("Total tests: 3");
                result.StdOut.Should().Contain("Passed: 2");
                result.StdOut.Should().Contain("Failed: 1");
                result.StdOut.Should().Contain("\u221a TestNamespace.VSTestXunitTests.VSTestXunitPassTestDesktop");

                // for target framework netcoreapp1.0
                result.StdOut.Should().Contain("Total tests: 3");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Failed: 2");
                result.StdOut.Should().Contain("X TestNamespace.VSTestXunitTests.VSTestXunitFailTestNetCoreApp");
            }

            result.ExitCode.Should().Be(1);
        }

        [WindowsOnlyFact]
        public void ItCreatesTwoCoverageFilesForMultiTargetedProject()
        {
            // Copy XunitMulti project in output directory of project dotnet-test.Tests
            string testAppName = "XunitMulti";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, identifier: "3")
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            string resultsDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete resultsDirectory if it exist
            if (Directory.Exists(resultsDirectory))
            {
                Directory.Delete(resultsDirectory, true);
            }

            // Call test
            CommandResult result = new DotnetTestCommand(Log, ConsoleLoggerOutputNormal)
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .Execute("--collect", "Code Coverage", "--results-directory", resultsDirectory);

            // Verify
            DirectoryInfo d = new DirectoryInfo(resultsDirectory);
            FileInfo[] coverageFileInfos = d.GetFiles("*.coverage", SearchOption.AllDirectories);
            Assert.Equal(2, coverageFileInfos.Length);
        }

        [Fact]
        public void ItCanTestAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = _testAssetsManager.CopyTestAsset(
                    "MultiTFMXunitProject",
                    testAssetSubdirectory: TestAssetSubdirectories.DesktopTestProjects)
                .WithSource();

            string projectDirectory = Path.Combine(testInstance.Path, "XUnitProject");

            new DotnetTestCommand(Log, ConsoleLoggerOutputNormal)
               .WithWorkingDirectory(projectDirectory)
               .Execute("--framework", "netcoreapp3.0")
               .Should().Pass();
        }
    }
}
