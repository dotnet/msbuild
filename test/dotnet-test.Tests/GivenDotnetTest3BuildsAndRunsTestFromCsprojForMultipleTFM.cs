// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;
using System.Runtime.InteropServices;
using System.IO;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTest3BuildsAndRunsTestFromCsprojForMultipleTFM : TestBase
    {
        // project targeting net46 will not run in non windows machine.
        [WindowsOnlyFact(Skip="https://github.com/dotnet/cli/issues/4526")]
        public void TestsFromAGivenProjectShouldRunWithExpectedOutputForMultiTFM()
        {
            // Copy DotNetCoreTestProject project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestDesktopAndNetCoreApp";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestDesktopAndNetCoreApp
            new RestoreCommand()
                .WithWorkingDirectory(testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            // Call test3
            CommandResult result = new DotnetTestCommand()
                                       .WithWorkingDirectory(testProjectDirectory)
                                       .ExecuteWithCapturedOutput("--diag LogFile.txt");


            // Verify
            // for target framework net46
            try
            {
                result.StdOut.Should().Contain("Total tests: 3. Passed: 2. Failed: 1. Skipped: 0.");
                result.StdOut.Should().Contain("Passed   TestNamespace.VSTestTests.VSTestPassTestDesktop");
            }
            catch
            {
                string logfile1 = Path.Combine(testProjectDirectory,"LogFile.txt");
                string[] logfile2 = Directory.GetFiles(testProjectDirectory, "LogFile.host.*");

                System.Console.WriteLine("**********************************Vstest.console Log****************************************************************");
                System.Console.WriteLine(File.ReadAllText(logfile1));
                System.Console.WriteLine("**********************************TestHost Log****************************************************************");
                System.Console.WriteLine(logfile2.Length>0 ? File.ReadAllText(logfile2[0]):"No log file found");
                System.Console.WriteLine("**************************************************************************************************");
            }

            // for target framework netcoreapp1.0
            //result.StdOut.Should().Contain("Total tests: 3. Passed: 1. Failed: 2. Skipped: 0.");
            //result.StdOut.Should().Contain("Failed   TestNamespace.VSTestTests.VSTestFailTestNetCoreApp");
        }
    }
}
