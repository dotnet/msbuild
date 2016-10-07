// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.VSTest.Tests
{
    public class VSTestTests: TestBase
    {
        [Fact]
        public void TestsFromAGivenContainerShouldRunWithExpectedOutput()
        {
            // Copy DotNetCoreTestProject project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestDotNetCoreProject";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName).WithLockFiles();

            string testProjectDirectory = testInstance.TestRoot;

            new Restore3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            // Build project DotNetCoreTestProject
            new Build3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            // Prepare args to send vstest
            string configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            string testAdapterPath = Path.Combine(testProjectDirectory, "bin", configuration, "netcoreapp1.0");
            string outputDll = Path.Combine(testAdapterPath, $"{testAppName}.dll");
            string argsForVstest = string.Concat("\"", outputDll, "\"", " --TestAdapterPath:", "\"", testAdapterPath, "\"");

            // Call vstest
            CommandResult result = new VSTestCommand().ExecuteWithCapturedOutput(argsForVstest);

            // Verify
            result.StdOut.Should().Contain("Total tests: 2. Passed: 1. Failed: 1. Skipped: 0.");
            result.StdOut.Should().Contain("Passed   TestNamespace.VSTestTests.VSTestPassTest");
            result.StdOut.Should().Contain("Failed   TestNamespace.VSTestTests.VSTestFailTest");
        }
    }
}
