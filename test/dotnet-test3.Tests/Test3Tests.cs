// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Test3.Tests
{
    public class Test3Tests : TestBase
    {
        [Fact]
        public void TestsFromAGivenProjectShouldRunWithExpectedOutput()
        {
            // Copy DotNetCoreTestProject project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestDotNetCoreProject";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestDotNetCoreProject
            new Restore3Command()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            // Call test3
            CommandResult result = new Test3Command().WithWorkingDirectory(testProjectDirectory).ExecuteWithCapturedOutput("/p:TargetFramework=netcoreapp1.0");

            // Verify
            result.StdOut.Should().Contain("Total tests: 2. Passed: 1. Failed: 1. Skipped: 0.");
            result.StdOut.Should().Contain("Passed   TestNamespace.VSTestTests.VSTestPassTest");
            result.StdOut.Should().Contain("Failed   TestNamespace.VSTestTests.VSTestFailTest");
        }
    }
}