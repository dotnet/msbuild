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
            // Copy VSTestDotNetCoreProject project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestDotNetCoreProject";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestDotNetCoreProject
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
        }

        [Fact]
        public void XunitSingleTFM()
        {
            // Copy VSTestXunitDotNetCoreProject project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestXunitDotNetCoreProject";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestXunitDotNetCoreProject
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
        }

        [Fact]
        public void TestWillNotBuildTheProjectIfNoBuildArgsIsGiven()
        {
            // Copy VSTestDotNetCoreProject project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestDotNetCoreProject";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestDotNetCoreProject
            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            string configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            string expectedError = Path.Combine(testProjectDirectory, "bin",
                                   configuration, "netcoreapp1.0", "VSTestDotNetCoreProject.dll");
            expectedError = "The test source file " + "\"" + expectedError + "\"" + " provided was not found.";

            // Call test
            CommandResult result = new DotnetTestCommand()
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .ExecuteWithCapturedOutput("--noBuild");

            // Verify
            result.StdOut.Should().Contain(expectedError); 
        }
    }
}