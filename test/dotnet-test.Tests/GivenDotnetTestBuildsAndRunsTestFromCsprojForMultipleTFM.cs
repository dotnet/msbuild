// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;
using System.IO;
using System;
using Xunit;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestFromCsprojForMultipleTFM : TestBase
    {
        [WindowsOnlyFact]
        public void MStestMultiTFM()
        {
            var testProjectDirectory = TestAssets.Get("VSTestMulti")
                .CreateInstance("1")
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
                .ExecuteWithCapturedOutput(TestBase.ConsoleLoggerOutputNormal);

            if (!DotnetUnderTest.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 3. Passed: 2. Failed: 1. Skipped: 0.", "because .NET 4.6 tests will pass")
                         .And.Contain("Passed   TestNamespace.VSTestTests.VSTestPassTestDesktop", "because .NET 4.6 tests will pass")
                         .And.Contain("Total tests: 3. Passed: 1. Failed: 2. Skipped: 0.", "because netcoreapp2.1 tests will fail")
                         .And.Contain("Failed   TestNamespace.VSTestTests.VSTestFailTestNetCoreApp", "because netcoreapp2.1 tests will fail");
            }
            result.ExitCode.Should().Be(1);
        }

        [WindowsOnlyFact]
        public void XunitMultiTFM()
        {
            // Copy XunitMulti project in output directory of project dotnet-test.Tests
            string testAppName = "XunitMulti";
            var testInstance = TestAssets.Get(testAppName)
                            .CreateInstance("2")
                            .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            // Restore project XunitMulti
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
            if (!DotnetUnderTest.IsLocalized())
            {
                // for target framework net46
                result.StdOut.Should().Contain("Total tests: 3. Passed: 2. Failed: 1. Skipped: 0.");
                result.StdOut.Should().Contain("Passed   TestNamespace.VSTestXunitTests.VSTestXunitPassTestDesktop");

                // for target framework netcoreapp1.0
                result.StdOut.Should().Contain("Total tests: 3. Passed: 1. Failed: 2. Skipped: 0.");
                result.StdOut.Should().Contain("Failed   TestNamespace.VSTestXunitTests.VSTestXunitFailTestNetCoreApp");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void ItCanTestAMultiTFMProjectWithImplicitRestore()
        {
            var testInstance = TestAssets.Get(
                    TestAssetKinds.DesktopTestProjects,
                    "MultiTFMXunitProject")
                .CreateInstance()
                .WithSourceFiles();

            string projectDirectory = Path.Combine(testInstance.Root.FullName, "XUnitProject");

            new DotnetTestCommand()
               .WithWorkingDirectory(projectDirectory)
               .ExecuteWithCapturedOutput($"{TestBase.ConsoleLoggerOutputNormal} --framework netcoreapp2.1")
               .Should().Pass();
        }
    }
}
