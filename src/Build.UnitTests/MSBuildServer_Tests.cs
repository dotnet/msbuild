// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Engine.UnitTests
{
    public class MSBuildServer_Tests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly TestEnvironment _env;
        private static string printPidContents = @$"
<Project>
<UsingTask TaskName=""ProcessIdTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <Target Name='AccessPID'>
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
        </ProcessIdTask>
        <Message Text=""Server ID is $(PID)"" Importance=""High"" />
    </Target>
</Project>";
        private static string sleepingTaskContents = @$"
<Project>
<UsingTask TaskName=""SleepingTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <Target Name='Sleep'>
        <ProcessIdTask SleepTime=""100000"" />
    </Target>
</Project>";

        public MSBuildServer_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(_output);
        }

        public void Dispose() => _env.Dispose();

        [Fact]
        public void MSBuildServerTest()
        {
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success, out bool exitedWithoutTimeout, false, _output);
            success.ShouldBeTrue();
            exitedWithoutTimeout.ShouldBeTrue("The entrypoint node should die on its own before 5 seconds elapse.");
            int pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int pidOfServerProcess = ParseNumber(output, "Server ID is ");
            pidOfInitialProcess.ShouldNotBe(pidOfServerProcess, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");

            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, out exitedWithoutTimeout, false, _output);
            success.ShouldBeTrue();
            exitedWithoutTimeout.ShouldBeTrue("The entrypoint node should die on its own before 5 seconds elapse.");
            int newPidOfInitialProcess = ParseNumber(output, "Process ID is ");
            newPidOfInitialProcess.ShouldNotBe(pidOfServerProcess, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");
            newPidOfInitialProcess.ShouldNotBe(pidOfInitialProcess, "Process started by two MSBuild executions should be different.");
            pidOfServerProcess.ShouldBe(ParseNumber(output, "Server ID is "), "Node used by both the first and second build should be the same.");

            // Start long-lived task execution
            TransientTestFile sleepProject = _env.CreateFile("napProject.proj", sleepingTaskContents);
            RunnerUtilities.RunProcessAndGetOutput(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, sleepProject.Path, out _, out _, waitForExit: false);

            // Kill the server
            Process.GetProcessById(pidOfServerProcess).KillTree(1000);

            // Ensure that a new build can still succeed and that its server node is different.
            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, out exitedWithoutTimeout, false, _output);
            success.ShouldBeTrue();
            exitedWithoutTimeout.ShouldBeTrue("The entrypoint node should die on its own before 5 seconds elapse.");
            newPidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int newServerProcessId = ParseNumber(output, "Server ID is ");
            newPidOfInitialProcess.ShouldNotBe(pidOfInitialProcess, "Process started by two MSBuild executions should be different.");
            newPidOfInitialProcess.ShouldNotBe(newServerProcessId, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");
            pidOfServerProcess.ShouldNotBe(newServerProcessId, "Node used by both the first and second build should be the same.");
        }

        [Fact]
        public void VerifyMixedLegacyBehavior()
        {
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success, out bool exitedWithoutTimeout, false, _output);
            success.ShouldBeTrue();
            exitedWithoutTimeout.ShouldBeTrue("The entrypoint node should die on its own before 5 seconds elapse.");
            int pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int pidOfServerProcess = ParseNumber(output, "Server ID is ");
            pidOfInitialProcess.ShouldNotBe(pidOfServerProcess, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");

            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "");
            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, out exitedWithoutTimeout, false, _output);
            success.ShouldBeTrue();
            exitedWithoutTimeout.ShouldBeTrue("The entrypoint node should die on its own before 5 seconds elapse.");
            pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int pidOfNewserverProcess = ParseNumber(output, "Server ID is ");
            pidOfInitialProcess.ShouldBe(pidOfNewserverProcess, "We did not start a server node to execute the target, so its pid should be the same.");

            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, out exitedWithoutTimeout, false, _output);
            success.ShouldBeTrue();
            exitedWithoutTimeout.ShouldBeTrue("The entrypoint node should die on its own before 5 seconds elapse.");
            pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            pidOfNewserverProcess = ParseNumber(output, "Server ID is ");
            pidOfInitialProcess.ShouldNotBe(pidOfNewserverProcess, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");
            pidOfServerProcess.ShouldBe(pidOfNewserverProcess, "Server node should be the same as from earlier.");
        }

        [Fact]
        public void BuildsWhileBuildIsRunningOnServer()
        {
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            TransientTestFile sleepProject = _env.CreateFile("napProject.proj", sleepingTaskContents);

            int pidOfServerProcess = -1;
            try
            {
                // Start a server node and find its PID.
                string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success, out bool exitedWithoutTimeout, false, _output);
                pidOfServerProcess = ParseNumber(output, "Server ID is ");

                RunnerUtilities.RunProcessAndGetOutput(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, sleepProject.Path, out _, out _, waitForExit: false);

                _env.SetEnvironmentVariable("MSBUILDUSESERVER", "");
                output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, out exitedWithoutTimeout, false, _output);
                success.ShouldBeTrue();
                exitedWithoutTimeout.ShouldBeTrue();
                ParseNumber(output, "Server ID is ").ShouldBe(ParseNumber(output, "Process ID is "), "There should not be a server node for this build.");

                _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
                output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, out exitedWithoutTimeout, false, _output);
                success.ShouldBeTrue();
                exitedWithoutTimeout.ShouldBeTrue();
                pidOfServerProcess.ShouldBe(ParseNumber(output, "Server ID is "), "Server should be the same as before.");
                pidOfServerProcess.ShouldNotBe(ParseNumber(output, "Process ID is "), "There should be a server node for this build.");
            }
            finally
            {
                if (pidOfServerProcess > -1)
                {
                    Process.GetProcessById(pidOfServerProcess).KillTree(1000);
                }
            }
        }

        [Fact]
        public void MultiProcBuildOnServer()
        {
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            TransientTestFile project = _env.CreateFile("test.proj", printPidContents);

            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success);
            success.ShouldBeTrue();
            int serverPid = ParseNumber(output, "Server ID is ");

            _env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");

            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path + " -m:2", out success);
            success.ShouldBeTrue();
            int workerPid = ParseNumber(output, "Server ID is ");
            workerPid.ShouldNotBe(serverPid);

            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path + " -m:2", out success);
            success.ShouldBeTrue();
            workerPid.ShouldBe(ParseNumber(output, "Server ID is "));
        }

        private int ParseNumber(string searchString, string toFind)
        {
            int indexOfId = searchString.IndexOf(toFind) + toFind.Length;
            int endOfId = searchString.IndexOf('\r', indexOfId);
            return int.Parse(searchString.Substring(indexOfId, endOfId - indexOfId));
        }
    }
}
