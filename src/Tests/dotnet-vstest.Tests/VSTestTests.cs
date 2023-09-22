// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;

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
            var testAsset = _testAssetsManager.CopyTestAsset(testAppName, identifier: "VSTestTests")
                .WithSource()
                .WithVersionVariables();

            var testRoot = testAsset.Path;

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(buildCommand.GetOutputDirectory(configuration: configuration).FullName, $"{testAppName}.dll");

            // Call vstest
            var result = new DotnetVSTestCommand(Log)
                .Execute(outputDll, "--logger:console;verbosity=normal");
            if (!TestContext.IsLocalized())
            {
                result.StdOut
                    .Should().Contain("Total tests: 2")
                    .And.Contain("Passed: 1")
                    .And.Contain("Failed: 1")
                    .And.Contain("Passed VSTestPassTest")
                    .And.Contain("Failed VSTestFailTest");
            }

            result.ExitCode.Should().Be(1);
        }

        [Fact]
        public void GivenADllAndMultipleTestRunParametersItPassesThemToVStestConsoleInTheCorrectFormat()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp("1");

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(Log, testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(OutputPathCalculator.FromProject(testProjectDirectory).GetOutputDirectory(configuration: configuration), "VSTestTestRunParameters.dll");

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
                result.StdOut.Should().Contain("Passed VSTestTestRunParameters");
            }

            result.ExitCode.Should().Be(0);
        }

        [Fact]
        public void ItShouldSetDotnetRootToLocationOfDotnetExecutable()
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

            var outputDll = Path.Combine(testRoot, "bin", configuration, ToolsetInfo.CurrentTargetFramework, $"{testAppName}.dll");

            // Call vstest
            var result = new DotnetVSTestCommand(Log)
                .Execute(outputDll, "--logger:console;verbosity=normal");

            result.ExitCode.Should().Be(1);
            var dotnet = result.StartInfo.FileName;
            Path.GetFileNameWithoutExtension(dotnet).Should().Be("dotnet");
            string dotnetRoot = Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)";
            result.StartInfo.EnvironmentVariables.ContainsKey(dotnetRoot).Should().BeTrue($"because {dotnetRoot} should be set");
            result.StartInfo.EnvironmentVariables[dotnetRoot].Should().Be(Path.GetDirectoryName(dotnet));
        }

        [Fact]
        public void ItShouldAcceptMultipleLoggers()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(Log, testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, ToolsetInfo.CurrentTargetFramework, "VSTestTestRunParameters.dll");

            var logFileName = $"{Path.GetTempFileName()}.trx";
            // Call test
            CommandResult result = new DotnetVSTestCommand(Log)
                                        .Execute(new[] {
                                            outputDll,
                                            "--logger:console;verbosity=normal",
                                            $"--logger:trx;LogFileName={logFileName}",
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
                result.StdOut.Should().Contain("Passed VSTestTestRunParameters");
            }
            result.ExitCode.Should().Be(0, $"Should have executed successfully, but got: {result.StdOut}");

            var testResultsDirectory = new FileInfo(Path.Combine(Environment.CurrentDirectory, "TestResults", logFileName));
            testResultsDirectory.Exists.Should().BeTrue("expected the test results file to be created");
        }

        [Fact]
        public void ItShouldAcceptNoLoggers()
        {
            var testProjectDirectory = CopyAndRestoreVSTestDotNetCoreTestApp();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            new BuildCommand(Log, testProjectDirectory)
                .Execute()
                .Should().Pass();

            var outputDll = Path.Combine(testProjectDirectory, "bin", configuration, ToolsetInfo.CurrentTargetFramework, "VSTestTestRunParameters.dll");

            // Call test
            CommandResult result = new DotnetVSTestCommand(Log)
                                        .Execute(new[] {
                                            outputDll,
                                            "--",
                                            "TestRunParameters.Parameter(name=\"myParam\",",
                                            "value=\"value\")",
                                            "TestRunParameters.Parameter(name=\"myParam2\",",
                                            "value=\"value",
                                            "with",
                                            "space\")"
                                        });

            //Verify
            // since there are no loggers, all we have to go on it the exit code
            result.ExitCode.Should().Be(0, $"Should have executed successfully, but got: {result.StdOut}");
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
