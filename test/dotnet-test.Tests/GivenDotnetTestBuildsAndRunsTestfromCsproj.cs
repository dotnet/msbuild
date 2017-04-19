// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using System;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnettestBuildsAndRunsTestfromCsproj : TestBase
    {
        [Fact]
        public void MSTestSingleTFM()
        {
            // Copy VSTestDotNetCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestDotNetCore";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            // Restore project VSTestDotNetCore
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
            result.StdOut.Should().Contain("Total tests: 2. Passed: 1. Failed: 1. Skipped: 0.");
            result.StdOut.Should().Contain("Passed   TestNamespace.VSTestTests.VSTestPassTest");
            result.StdOut.Should().Contain("Failed   TestNamespace.VSTestTests.VSTestFailTest");
            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void XunitSingleTFM()
        {
            // Copy VSTestXunitDotNetCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestXunitDotNetCore";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance()
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            // Restore project VSTestXunitDotNetCore
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
            result.StdOut.Should().Contain("Total tests: 2. Passed: 1. Failed: 1. Skipped: 0.");
            result.StdOut.Should().Contain("Passed   TestNamespace.VSTestXunitTests.VSTestXunitPassTest");
            result.StdOut.Should().Contain("Failed   TestNamespace.VSTestXunitTests.VSTestXunitFailTest");
            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void TestWillNotBuildTheProjectIfNoBuildArgsIsGiven()
        {
            // Copy and restore VSTestDotNetCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp();
            string configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            string expectedError = Path.Combine(testProjectDirectory, "bin",
                                   configuration, "netcoreapp2.0", "VSTestDotNetCore.dll");
            expectedError = "The test source file " + "\"" + expectedError + "\"" + " provided was not found.";

            // Call test
            CommandResult result = new DotnetTestCommand()
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .ExecuteWithCapturedOutput("--no-build");

            // Verify
            result.StdErr.Should().Contain(expectedError);
        }

        [Fact]
        public void TestWillCreateTrxLoggerInTheSpecifiedResultsDirectoryBySwitch()
        {
            // Copy and restore VSTestDotNetCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp();

            string trxLoggerDirectory = Path.Combine(testProjectDirectory, "TestResults", "netcoreappx.y");

            // Delete trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }

            // Call test with trx logger enabled and results directory explicitly specified.
            CommandResult result = new DotnetTestCommand()
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .ExecuteWithCapturedOutput("--logger trx -r \"" + trxLoggerDirectory + "\"");

            // Verify
            String[] trxFiles = Directory.GetFiles(trxLoggerDirectory, "*.trx");
            Assert.Equal(1, trxFiles.Length);
            result.StdOut.Should().Contain(trxFiles[0]);

            // Cleanup trxLoggerDirectory if it exist
            if(Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }
        }

        [Fact]
        public void ItCreatesTrxReportInTheSpecifiedResultsDirectoryByArgs()
        {
            // Copy and restore VSTestDotNetCore project in output directory of project dotnet-vstest.Tests
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp();

            string trxLoggerDirectory = Path.Combine(testProjectDirectory, "ResultsDirectory");

            // Delete trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }

            // Call test with logger enable
            CommandResult result = new DotnetTestCommand()
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .ExecuteWithCapturedOutput("--logger \"trx;logfilename=custom.trx\" -- RunConfiguration.ResultsDirectory=" + trxLoggerDirectory);

            // Verify
            var trxFilePath = Path.Combine(trxLoggerDirectory, "custom.trx");
            Assert.True(File.Exists(trxFilePath));
            result.StdOut.Should().Contain(trxFilePath);

            // Cleanup trxLoggerDirectory if it exist
            if (Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }
        }

        [Fact(Skip = "https://github.com/dotnet/cli/issues/5035")]
        public void ItBuildsAndTestsAppWhenRestoringToSpecificDirectory()
        {
            var rootPath = TestAssets.Get("VSTestDotNetCore").CreateInstance().WithSourceFiles().Root.FullName;

            string dir = "pkgs";
            string fullPath = Path.GetFullPath(Path.Combine(rootPath, dir));

            string args = $"--packages \"{dir}\"";
            new RestoreCommand()
                .WithWorkingDirectory(rootPath)
                .Execute(args)
                .Should()
                .Pass();

            new BuildCommand()
                .WithWorkingDirectory(rootPath)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass()
                .And.NotHaveStdErr();

            CommandResult result = new DotnetTestCommand()
                                        .WithWorkingDirectory(rootPath)
                                        .ExecuteWithCapturedOutput(TestBase.ConsoleLoggerOutputNormal);

            result.StdOut.Should().Contain("Total tests: 2. Passed: 1. Failed: 1. Skipped: 0.");
            result.StdOut.Should().Contain("Passed   TestNamespace.VSTestTests.VSTestPassTest");
            result.StdOut.Should().Contain("Failed   TestNamespace.VSTestTests.VSTestFailTest");
        }

        private string CopyAndRestoreVSTestDotNetCoreTestApp([CallerMemberName] string callingMethod = "")
        {
            // Copy VSTestDotNetCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestDotNetCore";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance(callingMethod)
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            // Restore project VSTestDotNetCore
            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();
            return testProjectDirectory;
        }
    }
}
