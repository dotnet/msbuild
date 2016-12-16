// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using System;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnettestBuildsAndRunsTestfromCsproj : TestBase
    {
        [Fact]
        public void MSTestSingleTFM()
        {
            // Copy VSTestDotNetCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestDotNetCore";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestDotNetCore
            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            // Call test
            CommandResult result = new DotnetTestCommand()
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .ExecuteWithCapturedOutput();

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
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestXunitDotNetCore
            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            // Call test
            CommandResult result = new DotnetTestCommand()
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .ExecuteWithCapturedOutput();

            // Verify
            result.StdOut.Should().Contain("Total tests: 2. Passed: 1. Failed: 1. Skipped: 0.");
            result.StdOut.Should().Contain("Passed   TestNamespace.VSTestXunitTests.VSTestXunitPassTest");
            result.StdOut.Should().Contain("Failed   TestNamespace.VSTestXunitTests.VSTestXunitFailTest");
            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void TestWillNotBuildTheProjectIfNoBuildArgsIsGiven()
        {
            // Copy VSTestDotNetCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestDotNetCore";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestDotNetCore
            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            string configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            string expectedError = Path.Combine(testProjectDirectory, "bin",
                                   configuration, "netcoreapp1.0", "VSTestDotNetCore.dll");
            expectedError = "The test source file " + "\"" + expectedError + "\"" + " provided was not found.";

            // Call test
            CommandResult result = new DotnetTestCommand()
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .ExecuteWithCapturedOutput("--no-build");

            // Verify
            result.StdOut.Should().Contain(expectedError); 
        }

        [Fact]
        public void TestWillCreateTrxLogger()
        {
            // Copy VSTestDotNetCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestDotNetCore";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestDotNetCore
            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            string trxLoggerDirectory = Path.Combine(testProjectDirectory, "TestResults");

            // Delete trxLoggerDirectory if it exist
            if(Directory.Exists(trxLoggerDirectory))
            {
                Directory.Delete(trxLoggerDirectory, true);
            }

            // Call test with logger enable
            CommandResult result = new DotnetTestCommand()
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .ExecuteWithCapturedOutput("--logger:trx");

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
    }
}
