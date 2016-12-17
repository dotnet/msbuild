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
    public class GivenDotnetTestBuildsAndRunsTestFromCsprojForMultipleTFM : TestBase
    {
        [WindowsOnlyFact(Skip="https://github.com/dotnet/cli/issues/4616")]
        public void MStestMultiTFM()
        {
            var testProjectDirectory = TestAssets.Get("VSTestDesktopAndNetCore")
                .CreateInstance()
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
                .ExecuteWithCapturedOutput();

            result.StdOut
                .Should().Contain("Total tests: 3. Passed: 2. Failed: 1. Skipped: 0.", "because .NET 4.6 tests will pass")
                     .And.Contain("Passed   TestNamespace.VSTestTests.VSTestPassTestDesktop", "because .NET 4.6 tests will pass")
                     .And.Contain("Total tests: 3. Passed: 1. Failed: 2. Skipped: 0.", "because netcoreapp1.0 tests will fail")
                     .And.Contain("Failed   TestNamespace.VSTestTests.VSTestFailTestNetCoreApp", "because netcoreapp1.0 tests will fail");
            result.ExitCode.Should().Be(1);
        }

        [WindowsOnlyFact]
        public void XunitMultiTFM()
        {
            // Copy VSTestXunitDesktopAndNetCore project in output directory of project dotnet-test.Tests
            string testAppName = "VSTestXunitDesktopAndNetCore";
            TestInstance testInstance = TestAssetsManager.CreateTestInstance(testAppName);

            string testProjectDirectory = testInstance.TestRoot;

            // Restore project VSTestXunitDesktopAndNetCore
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
            result.ExitCode.Should().Be(1);
        }
    }
}
