// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using System;
using Xunit;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestFromCsprojForMultipleTFM : TestBase
    {
        [WindowsOnlyFact]
        public void MStestMultiTFM()
        {
            var testProjectDirectory = TestAssets.Get("VSTestMulti")
                .CreateInstance("1")
                .WithSourceFiles()
                .WithNuGetConfig(new RepoDirectoriesProvider().TestPackages)
                .Root;
            
            var runtime = DotnetLegacyRuntimeIdentifiers.InferLegacyRestoreRuntimeIdentifier();

            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .WithRuntime(runtime)
                .Execute()
                .Should().Pass();

            var result = new DotnetTestCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .WithRuntime(runtime)
                .ExecuteWithCapturedOutput(TestBase.ConsoleLoggerOutputNormal);

            if (!DotnetUnderTest.IsLocalized())
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
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance("2")
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            // Restore project XunitMulti
            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            // Call test
            CommandResult result = new DotnetTestCommand()
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .ExecuteWithCapturedOutput(TestBase.ConsoleLoggerOutputNormal);

            // Verify
            if (!DotnetUnderTest.IsLocalized())
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
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance("3")
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

             string resultsDirectory = Path.Combine(testProjectDirectory, "RD");

            // Delete resultsDirectory if it exist
            if (Directory.Exists(resultsDirectory))
            {
                Directory.Delete(resultsDirectory, true);
            }

            // Call test
            CommandResult result = new DotnetTestCommand()
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .ExecuteWithCapturedOutput($"{TestBase.ConsoleLoggerOutputNormal} --collect \"Code Coverage\" --results-directory {resultsDirectory}");

            // Verify
            DirectoryInfo d = new DirectoryInfo(resultsDirectory);
            FileInfo[] coverageFileInfos = d.GetFiles("*.coverage", SearchOption.AllDirectories);
            Assert.Equal(2, coverageFileInfos.Length);
        }

        [Fact]
        public void ItCanTestAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = TestAssets.Get(
                    TestAssetKinds.DesktopTestProjects,
                    "MultiTFMXunitProject")
                .CreateInstance()
                .WithSourceFiles();

            string projectDirectory = Path.Combine(testInstance.Root.FullName, "XUnitProject");

            new DotnetTestCommand()
               .WithWorkingDirectory(projectDirectory)
               .ExecuteWithCapturedOutput($"{TestBase.ConsoleLoggerOutputNormal} --framework netcoreapp3.0")
               .Should().Pass();
        }
    }
}
