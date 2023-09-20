// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;

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
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("2");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .WithWorkingDirectory(testProjectDirectory)
                                        .Execute(ConsoleLoggerOutputNormal.Concat(new[] {
                                            "--",
                                            "TestRunParameters.Parameter(name=\"myParam\",value=\"value\")",
                                            "TestRunParameters.Parameter(name=\"myParam2\",value=\"value with space\")"
                                        }));

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().NotMatch("The test run parameter argument '*' is invalid.");
                result.StdOut.Should().Contain("Total tests: 1");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Passed VSTestTestRunParameters");
            }

            result.ExitCode.Should().Be(0);
        }

        [Fact]
        public void GivenADllAndMultipleTestRunParametersItPassesThemToVStestConsoleInTheCorrectFormat()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("3");

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(Log, testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(OutputPathCalculator.FromProject(testProjectDirectory).GetOutputDirectory(configuration: configuration), "VSTestTestRunParameters.dll");

            // Call test
            CommandResult result = new DotnetTestCommand(Log)
                                        .Execute(ConsoleLoggerOutputNormal.Concat(new[] {
                                            outputDll,
                                            "--",
                                            "TestRunParameters.Parameter(name=\"myParam\",value=\"value\")",
                                            "TestRunParameters.Parameter(name=\"myParam2\",value=\"value with space\")"
                                        }));

            // Verify
            if (!TestContext.IsLocalized())
            {
                result.StdOut.Should().NotMatch("The test run parameter argument '*' is invalid.");
                result.StdOut.Should().Contain("Total tests: 1");
                result.StdOut.Should().Contain("Passed: 1");
                result.StdOut.Should().Contain("Passed VSTestTestRunParameters");
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
