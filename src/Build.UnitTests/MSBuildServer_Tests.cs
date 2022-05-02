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

        public MSBuildServer_Tests(ITestOutputHelper output)
        {
            _output = output;
            _env = TestEnvironment.Create(_output);
        }

        public void Dispose() => _env.Dispose();

        [Fact]
        public void MSBuildServerTest()
        {
            string contents = @$"
<Project>
<UsingTask TaskName=""ProcessIdTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <Target Name='AccessPID'>
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
        </ProcessIdTask>
        <Message Text=""Server ID is $(PID)"" Importance=""High"" />
    </Target>
</Project>";
            TransientTestFile project = _env.CreateFile("testProject.proj", contents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success, out bool exitedWithoutTimeout, false, _output);
            success.ShouldBeTrue();
            exitedWithoutTimeout.ShouldBeTrue("The entrypoint node should die on its own before 5 seconds elapse.");
            int indexOfId = output.IndexOf("Process ID is ") + "Process ID is ".Length;
            int endOfId = output.IndexOf('\r', indexOfId);
            int pidOfInitialProcess = int.Parse(output.Substring(indexOfId, endOfId - indexOfId));
            indexOfId = output.IndexOf("Server ID is ") + "Server ID is ".Length;
            endOfId = output.IndexOf('\n', indexOfId);
            int pidOfServerProcess = int.Parse(output.Substring(indexOfId, endOfId - indexOfId));
            pidOfInitialProcess.ShouldNotBe(pidOfServerProcess, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");

            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, out exitedWithoutTimeout, false, _output);
            success.ShouldBeTrue();
            exitedWithoutTimeout.ShouldBeTrue("The entrypoint node should die on its own before 5 seconds elapse.");
            indexOfId = output.IndexOf("Process ID is ") + "Process ID is ".Length;
            endOfId = output.IndexOf('\r', indexOfId);
            int newPidOfInitialProcess = int.Parse(output.Substring(indexOfId, endOfId - indexOfId));
            indexOfId = output.IndexOf("Server ID is ") + "Server ID is ".Length;
            endOfId = output.IndexOf('\n', indexOfId);
            newPidOfInitialProcess.ShouldNotBe(pidOfServerProcess, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");
            newPidOfInitialProcess.ShouldNotBe(pidOfInitialProcess, "Process started by two MSBuild executions should be different.");
            pidOfServerProcess.ShouldBe(int.Parse(output.Substring(indexOfId, endOfId - indexOfId)), "Node used by both the first and second build should be the same.");

            // Start long-lived task execution
            contents = @$"
<Project>
<UsingTask TaskName=""SleepingTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <Target Name='Sleep'>
        <ProcessIdTask SleepTime=""100000"" />
    </Target>
</Project>";
            TransientTestFile sleepProject = _env.CreateFile("napProject.proj", contents);
            RunnerUtilities.RunProcessAndGetOutput(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, sleepProject.Path, out _, out _, waitForExit: false);

            // Kill the server
            Process.GetProcessById(pidOfServerProcess).KillTree(1000);

            // Ensure that a new build can still succeed and that its server node is different.
            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, out exitedWithoutTimeout, false, _output);
            success.ShouldBeTrue();
            exitedWithoutTimeout.ShouldBeTrue("The entrypoint node should die on its own before 5 seconds elapse.");
            indexOfId = output.IndexOf("Process ID is ") + "Process ID is ".Length;
            endOfId = output.IndexOf('\r', indexOfId);
            newPidOfInitialProcess = int.Parse(output.Substring(indexOfId, endOfId - indexOfId));
            indexOfId = output.IndexOf("Server ID is ") + "Server ID is ".Length;
            endOfId = output.IndexOf('\n', indexOfId);
            newPidOfInitialProcess.ShouldNotBe(pidOfInitialProcess, "Process started by two MSBuild executions should be different.");
            int newServerProcessId = int.Parse(output.Substring(indexOfId, endOfId - indexOfId));
            newPidOfInitialProcess.ShouldNotBe(newServerProcessId, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");
            pidOfServerProcess.ShouldNotBe(newServerProcessId, "Node used by both the first and second build should be the same.");
        }
    }
}
