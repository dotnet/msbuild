// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using System;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTest3BuildsAndRunsTestFromCsprojForMultipleTFM : TestBase
    {
        // project targeting net46 will not run in non windows machine.
        [WindowsOnlyFact]
        public void MStestMultiTFM()
        {
            // Copy VSTestDesktopAndNetCoreApp project in output directory of project dotnet-test.Tests
            string testAppName = "VSTestDesktopAndNetCoreApp";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestDesktopAndNetCoreApp
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
            // for target framework net46
            result.StdOut.Should().Contain("Total tests: 3. Passed: 2. Failed: 1. Skipped: 0.");
            result.StdOut.Should().Contain("Passed   TestNamespace.VSTestTests.VSTestPassTestDesktop");

            // for target framework netcoreapp1.0
            result.StdOut.Should().Contain("Total tests: 3. Passed: 1. Failed: 2. Skipped: 0.");
            result.StdOut.Should().Contain("Failed   TestNamespace.VSTestTests.VSTestFailTestNetCoreApp");
        }

        [WindowsOnlyFact]
        public void XunitMultiTFM()
        {
            // Copy VSTestXunitDesktopAndNetCoreApp project in output directory of project dotnet-test.Tests
            string testAppName = "VSTestXunitDesktopAndNetCoreApp";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestXunitDesktopAndNetCoreApp
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
            // for target framework net46
            result.StdOut.Should().Contain("Total tests: 3. Passed: 2. Failed: 1. Skipped: 0.");
            result.StdOut.Should().Contain("Passed   TestNamespace.VSTestXunitTests.VSTestXunitPassTestDesktop");

            // for target framework netcoreapp1.0
            result.StdOut.Should().Contain("Total tests: 3. Passed: 1. Failed: 2. Skipped: 0.");
            result.StdOut.Should().Contain("Failed   TestNamespace.VSTestXunitTests.VSTestXunitFailTestNetCoreApp");
        }
    }
}
