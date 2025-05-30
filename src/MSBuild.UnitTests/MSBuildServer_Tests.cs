// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.Debugging;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
#if NETFRAMEWORK
using Microsoft.IO;
#else
using System.IO;
#endif
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Path = System.IO.Path;

namespace Microsoft.Build.Engine.UnitTests
{
    public class SleepingTask : Microsoft.Build.Utilities.Task
    {
        public int SleepTime { get; set; }

        /// <summary>
        /// Sleep for SleepTime milliseconds.
        /// </summary>
        /// <returns>Success on success.</returns>
        public override bool Execute()
        {
            Thread.Sleep(SleepTime);
            return !Log.HasLoggedErrors;
        }
    }

    public class ProcessIdTask : Microsoft.Build.Utilities.Task
    {
        [Output]
        public int Pid { get; set; }

        /// <summary>
        /// Log the id for this process.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Pid = Process.GetCurrentProcess().Id;
            return true;
        }
    }

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
        private static string sleepingTaskContentsFormat = @$"
<Project>
<UsingTask TaskName=""SleepingTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <Target Name='Sleep'>
        <!-- create a marker file that represents the build is started. -->
        <WriteLinesToFile File=""{{0}}"" />
        <SleepingTask SleepTime=""100000"" />
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
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success, false, _output);
            success.ShouldBeTrue();
            int pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int pidOfServerProcess = ParseNumber(output, "Server ID is ");
            pidOfInitialProcess.ShouldNotBe(pidOfServerProcess, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");

            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, false, _output);
            success.ShouldBeTrue();
            int newPidOfInitialProcess = ParseNumber(output, "Process ID is ");
            newPidOfInitialProcess.ShouldNotBe(pidOfServerProcess, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");
            newPidOfInitialProcess.ShouldNotBe(pidOfInitialProcess, "Process started by two MSBuild executions should be different.");
            pidOfServerProcess.ShouldBe(ParseNumber(output, "Server ID is "), "Node used by both the first and second build should be the same.");

            // Prep to kill the long-lived task we're about to start.
            TransientTestFile markerFile = _env.ExpectFile();
            string? dir = Path.GetDirectoryName(markerFile.Path);
            using var watcher = new System.IO.FileSystemWatcher(dir!);
            watcher.Created += (o, e) =>
            {
                _output.WriteLine($"The marker file {markerFile.Path} was created. The build task has been started. Ready to kill the server.");
                // Kill the server
                Process.GetProcessById(pidOfServerProcess).KillTree(1000);
                _output.WriteLine($"The old server was killed.");
            };
            watcher.Filter = Path.GetFileName(markerFile.Path);
            watcher.EnableRaisingEvents = true;

            // Start long-lived task execution
            TransientTestFile sleepProject = _env.CreateFile("napProject.proj", string.Format(sleepingTaskContentsFormat, markerFile.Path));
            RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, sleepProject.Path, out _);

            // Ensure that a new build can still succeed and that its server node is different.
            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, false, _output);

            success.ShouldBeTrue();
            newPidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int newServerProcessId = ParseNumber(output, "Server ID is ");
            // Register process to clean up (be killed) after tests ends.
            _env.WithTransientProcess(newServerProcessId);
            newPidOfInitialProcess.ShouldNotBe(pidOfInitialProcess, "Process started by two MSBuild executions should be different.");
            newPidOfInitialProcess.ShouldNotBe(newServerProcessId, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");
            pidOfServerProcess.ShouldNotBe(newServerProcessId, "Node used by both the first and second build should not be the same.");
        }

        [Fact]
        public void VerifyMixedLegacyBehavior()
        {
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success, false, _output);
            success.ShouldBeTrue();
            int pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int pidOfServerProcess = ParseNumber(output, "Server ID is ");
            // Register process to clean up (be killed) after tests ends.
            _env.WithTransientProcess(pidOfServerProcess);
            pidOfInitialProcess.ShouldNotBe(pidOfServerProcess, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");

            Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "");
            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, false, _output);
            success.ShouldBeTrue();
            pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int pidOfNewserverProcess = ParseNumber(output, "Server ID is ");
            pidOfInitialProcess.ShouldBe(pidOfNewserverProcess, "We did not start a server node to execute the target, so its pid should be the same.");

            Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, false, _output);
            success.ShouldBeTrue();
            pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            pidOfNewserverProcess = ParseNumber(output, "Server ID is ");
            pidOfInitialProcess.ShouldNotBe(pidOfNewserverProcess, "We started a server node to execute the target rather than running it in-proc, so its pid should be different.");
            pidOfServerProcess.ShouldBe(pidOfNewserverProcess, "Server node should be the same as from earlier.");

            if (pidOfServerProcess != pidOfNewserverProcess)
            {
                // Register process to clean up (be killed) after tests ends.
                _env.WithTransientProcess(pidOfNewserverProcess);
            }
        }

        [Fact]
        public void BuildsWhileBuildIsRunningOnServer()
        {
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);

            TransientTestFile markerFile = _env.ExpectFile();
            TransientTestFile sleepProject = _env.CreateFile("napProject.proj", string.Format(sleepingTaskContentsFormat, markerFile.Path));

            int pidOfServerProcess;
            Task t;
            // Start a server node and find its PID.
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success, false, _output);
            pidOfServerProcess = ParseNumber(output, "Server ID is ");
            _env.WithTransientProcess(pidOfServerProcess);

            string? dir = Path.GetDirectoryName(markerFile.Path);
            using var watcher = new System.IO.FileSystemWatcher(dir!);
            ManualResetEvent mre = new ManualResetEvent(false);
            watcher.Created += (o, e) =>
            {
                _output.WriteLine($"The marker file {markerFile.Path} was created. The build task has been started.");
                mre.Set();
            };
            watcher.Filter = Path.GetFileName(markerFile.Path);
            watcher.EnableRaisingEvents = true;
            t = Task.Run(() =>
            {
                RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, sleepProject.Path, out _, false, _output);
            });

            // The server will soon be in use; make sure we don't try to use it before that happens.
            _output.WriteLine("Waiting for the server to be in use.");
            mre.WaitOne();
            _output.WriteLine("It's OK to go ahead.");

            Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "0");

            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, false, _output);
            success.ShouldBeTrue();
            ParseNumber(output, "Server ID is ").ShouldBe(ParseNumber(output, "Process ID is "), "There should not be a server node for this build.");

            Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out success, false, _output);
            success.ShouldBeTrue();
            pidOfServerProcess.ShouldNotBe(ParseNumber(output, "Server ID is "), "The server should be otherwise occupied.");
            pidOfServerProcess.ShouldNotBe(ParseNumber(output, "Process ID is "), "There should not be a server node for this build.");
            ParseNumber(output, "Server ID is ").ShouldBe(ParseNumber(output, "Process ID is "), "Process ID and Server ID should coincide.");

            // Clean up process and tasks
            // 1st kill registered processes
            _env.Dispose();
            // 2nd wait for sleep task which will ends as soon as the process is killed above.
            t.Wait();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanShutdownServerProcess(bool byBuildManager)
        {
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);

            // Start a server node and find its PID.
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success, false, _output);
            success.ShouldBeTrue();
            int pidOfServerProcess = ParseNumber(output, "Server ID is ");
            _env.WithTransientProcess(pidOfServerProcess);

            var serverProcess = Process.GetProcessById(pidOfServerProcess);

            serverProcess.HasExited.ShouldBeFalse();

            if (byBuildManager)
            {
                BuildManager.DefaultBuildManager.ShutdownAllNodes();
            }
            else
            {
                bool serverIsDown = MSBuildClient.ShutdownServer(CancellationToken.None);
                serverIsDown.ShouldBeTrue();
            }

            serverProcess.WaitForExit(10_000);

            serverProcess.HasExited.ShouldBeTrue();
        }

        [Fact]
        public void CanShutdownServerProcessWhenNotRunning()
        {
            bool serverIsDown = MSBuildClient.ShutdownServer(CancellationToken.None);
            serverIsDown.ShouldBeTrue();
        }

        [Fact]
        public void ServerShouldNotRunWhenNodeReuseEqualsFalse()
        {
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path + " /nodereuse:false", out bool success, false, _output);
            success.ShouldBeTrue();
            int pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int pidOfServerProcess = ParseNumber(output, "Server ID is ");
            pidOfInitialProcess.ShouldBe(pidOfServerProcess, "We started a server node even when nodereuse is false.");
        }

        [Fact]
        public void ServerShouldNotStartWhenBuildIsInteractive()
        {
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path + " -interactive", out bool success, false, _output);
            int pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int pidOfServerProcess = ParseNumber(output, "Server ID is ");

            success.ShouldBeTrue();
            pidOfInitialProcess.ShouldBe(pidOfServerProcess, "We started a server node even when build is interactive.");
        }

        [Fact]
        public void PropertyMSBuildStartupDirectoryOnServer()
        {
            // This test seems to be flaky, lets enable better logging to investigate it next time
            // TODO: delete after investigated its flakiness
            _env.WithTransientDebugEngineForNewProcesses(true);

            string reportMSBuildStartupDirectoryProperty = @$"
<Project>
    <UsingTask TaskName=""ProcessIdTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
	<Target Name=""DisplayMessages"">
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
        </ProcessIdTask>
        <Message Text=""Server ID is $(PID)"" Importance=""High"" />
		<Message Text="":MSBuildStartupDirectory:$(MSBuildStartupDirectory):"" Importance=""high"" />
	</Target>
</Project>";

            TransientTestFile project = _env.CreateFile("testProject.proj", reportMSBuildStartupDirectoryProperty);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            // Start on current working directory
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"/t:DisplayMessages {project.Path}", out bool success, false, _output);
            success.ShouldBeTrue();
            int pidOfServerProcess = ParseNumber(output, "Server ID is ");
            _env.WithTransientProcess(pidOfServerProcess);
            output.ShouldContain($@":MSBuildStartupDirectory:{Environment.CurrentDirectory}:");

            // Start on transient project directory
            _env.SetCurrentDirectory(Path.GetDirectoryName(project.Path));
            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"/t:DisplayMessages {project.Path}", out success, false, _output);
            int pidOfNewServerProcess = ParseNumber(output, "Server ID is ");
            if (pidOfServerProcess != pidOfNewServerProcess)
            {
                // Register process to clean up (be killed) after tests ends.
                _env.WithTransientProcess(pidOfNewServerProcess);
            }
            pidOfNewServerProcess.ShouldBe(pidOfServerProcess);
            output.ShouldContain($@":MSBuildStartupDirectory:{Environment.CurrentDirectory}:");
        }

        private int ParseNumber(string searchString, string toFind)
        {
            Regex regex = new(@$"{toFind}(\d+)");
            Match match = regex.Match(searchString);
            return int.Parse(match.Groups[1].Value);
        }
    }
}
