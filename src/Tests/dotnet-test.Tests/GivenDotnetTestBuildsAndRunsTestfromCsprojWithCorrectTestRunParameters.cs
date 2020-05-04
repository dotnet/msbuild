// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;
using System;
using System.IO;

namespace Microsoft.DotNet.Cli.Test.Tests
{
    public class GivenDotnetTestBuildsAndRunsTestfromCsprojWithCorrectTestRunParameters : SdkTest
    {
        public GivenDotnetTestBuildsAndRunsTestfromCsprojWithCorrectTestRunParameters(ITestOutputHelper log) : base(log)
        {
        }

        private readonly string[] ConsoleLoggerOutputNormal = new[] { "--logger", "console;verbosity=normal" };

        [Fact]
        public void GivenAProjectAndMultipleTestRunParametersItPassesThemToVStestConsoleInTheCorrectFormat()
        {
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("1");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(ConsoleLoggerOutputNormal.Concat(new[] {
                                            "--",
                                            "TestRunParameters.Parameter(name=\"myParam\",",
                                            "value=\"value\")",
                                            "TestRunParameters.Parameter(name=\"myParam2\",",
                                            "value=\"value", 
                                            "with", 
                                            "space\")"
                                        }));

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

        [Fact]
        public void GivenADllAndMultipleTestRunParametersItPassesThemToVStestConsoleInTheCorrectFormat()
        {
            var testProjectDirectory = this.CopyAndRestoreVSTestDotNetCoreTestApp("2");

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(Log, testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, "netcoreapp3.0", "VSTestTestRunParameters.dll");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .Execute(ConsoleLoggerOutputNormal.Concat(new[] {
                                            outputDll,
                                            "--",
                                            "TestRunParameters.Parameter(name=\"myParam\",",
                                            "value=\"value\")",
                                            "TestRunParameters.Parameter(name=\"myParam2\",",
                                            "value=\"value",
                                            "with",
                                            "space\")"
                                        }));

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
            new RestoreCommand(Log, testProjectDirectory)
                .Execute()
                .Should()
                .Pass();

            return testProjectDirectory;
        }
    }
}
