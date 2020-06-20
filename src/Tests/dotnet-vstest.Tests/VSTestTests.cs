// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.VSTest.Tests
{
    public class VSTestTests : SdkTest
    {
        public VSTestTests(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void TestsFromAGivenContainerShouldRunWithExpectedOutput()
        {
            var testAppName = "VSTestCore";
            var testAsset = _testAssetsManager.CopyTestAsset(testAppName)
                .WithSource()
                .WithVersionVariables();

            var testRoot = testAsset.Path;

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(testAsset)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testRoot, "bin", configuration, "netcoreapp3.0", $"{testAppName}.dll");

            // Call vstest
            var result = new DotnetVSTestCommand(Log)
                .Execute(outputDll, "--logger:console;verbosity=normal");
            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 2")
                    .And.Contain("Passed: 1")
                    .And.Contain("Failed: 1")
                    .And.Contain("\u221a VSTestPassTest")
                    .And.Contain("X VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void GivenADllAndMultipleTestRunParametersItPassesThemToVStestConsoleInTheCorrectFormat()
        {
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("1");

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(Log, testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, "netcoreapp3.0", "VSTestTestRunParameters.dll");

            // Call test
            CommandResult result = new DotnetVSTestCommand(Log)
                                        .Execute(new[] {
                                            outputDll,
                                            "--logger:console;verbosity=normal",
                                            "--",
                                            "TestRunParameters.Parameter(name=\"myParam\",",
                                            "value=\"value\")",
                                            "TestRunParameters.Parameter(name=\"myParam2\",",
                                            "value=\"value",
                                            "with",
                                            "space\")"
                                        });

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().NotMatch("The test run parameter argument '*' is invalid.");
                result.StdOut.Should().Contain("Total tests: 1");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("\u221a VSTestTestRunParameters");
            }

            result.ExitCode.Should().Be(0);
        }

        private string CopyAndRestoreVSTestDotNetCoreTestApp([CallerMemberName] string callingMethod = "")
        {
            // Copy VSTestCore project in output directory of project dotnet-vstest.Tests
            string testAppName = "VSTestTestRunParameters";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, callingMethod: callingMethod)
                            .WithSource()
                            .WithVersionVariables();

            var testProjectDirectory = testInstance.Path;

            // Restore project VSTestCore
            new RestoreCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            return testProjectDirectory;
        }
    }
}
