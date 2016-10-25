// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;
using System.Runtime.InteropServices;

namespace Microsoft.DotNet.Cli.Test3.Tests
{
    public class GivenDotnetTest3BuildsAndRunsTestFromCsprojForMultipleTFM : TestBase
    {
        [Fact]
        public void TestsFromAGivenProjectShouldRunWithExpectedOutputForMultiTFM()
        {
            // project targeting net46 will not run in non windows machine.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Copy DotNetCoreTestProject project in output directory of project dotnet-vstest.Tests
                string testAppName = "VSTestDesktopAndNetCoreApp";
                TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

                string testProjectDirectory = testInstance.TestRoot;

                // Restore project VSTestDotNetCoreProject
                new Restore3Command()
                    .WithWorkingDirectory(testProjectDirectory)
                    .Execute()
                    .Should()
                    .Pass();

                // Call test3
                CommandResult result = new Test3Command().WithWorkingDirectory(testProjectDirectory).ExecuteWithCapturedOutput("");

                // Verify
                // for target framework net46
                result.StdOut.Should().Contain("Total tests: 3. Passed: 2. Failed: 1. Skipped: 0.");
                result.StdOut.Should().Contain("Passed   TestNamespace.VSTestTests.VSTestPassTestDesktop");

                // for target framework netcoreapp1.0
                result.StdOut.Should().Contain("Total tests: 3. Passed: 1. Failed: 2. Skipped: 0.");
                result.StdOut.Should().Contain("Failed   TestNamespace.VSTestTests.VSTestFailTestNetCoreApp");
            }
        }
    }
}
