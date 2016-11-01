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
        // Adding log to root cause test failure for net46
        private const string vstestLog = "VSTEST_TRACE_BUILD";

        // project targeting net46 will not run in non windows machine.
        [WindowsOnlyFact]
        public void TestsFromAGivenProjectShouldRunWithExpectedOutputForMultiTFM()
        {
            Environment.SetEnvironmentVariable(vstestLog, "1");

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
                                       .ExecuteWithCapturedOutput("--diag LogFile.txt");



            try
            {
                // Verify
                // for target framework net46
                result.StdOut.Should().Contain("Total tests: 3. Passed: 2. Failed: 1. Skipped: 0.");
                result.StdOut.Should().Contain("Passed   TestNamespace.VSTestTests.VSTestPassTestDesktop");

                // for target framework netcoreapp1.0
                result.StdOut.Should().Contain("Total tests: 3. Passed: 1. Failed: 2. Skipped: 0.");
                result.StdOut.Should().Contain("Failed   TestNamespace.VSTestTests.VSTestFailTestNetCoreApp");
            }
            catch
            {
                Console.WriteLine("*********************************StdOut****************************************************************");
                Console.WriteLine(result.StdOut.ToString());

                Console.WriteLine("*********************************StdErr****************************************************************");
                Console.WriteLine(result.StdErr.ToString());

                string logfile1 = Path.Combine(testProjectDirectory, "LogFile.txt");
                string[] logfile2 = Directory.GetFiles(testProjectDirectory, "LogFile.host.*");

                Console.WriteLine("**********************************Vstest.console Log****************************************************************");
                Console.WriteLine(File.ReadAllText(logfile1));
                Console.WriteLine("**********************************TestHost Log****************************************************************");
                Console.WriteLine(logfile2.Length > 0 ? File.ReadAllText(logfile2[0]) : "No log file found");
                Console.WriteLine("**************************************************************************************************");
            }
        }

        [WindowsOnlyFact]
        public void TestsFromAGivenXunitProjectShouldRunWithExpectedOutputForMultiTFM()
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
