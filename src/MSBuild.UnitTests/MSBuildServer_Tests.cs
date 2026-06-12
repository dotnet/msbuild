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
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
#if NETFRAMEWORK
using Microsoft.IO;
#else
using System.IO;
#endif
using Shouldly;
using Xunit;
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

    // Marked multithreadable so that under /mt the engine runs it in-process on a server thread
    // (rather than routing it to a sidecar TaskHost), letting it observe the server process's GC mode.
    // The task only reads its own PID and GCSettings, so it is genuinely thread-safe.
    [Microsoft.Build.Framework.MSBuildMultiThreadableTask]
    public class ProcessIdTask : Microsoft.Build.Utilities.Task
    {
        [Output]
        public int Pid { get; set; }

        [Output]
        public bool IsServerGC { get; set; }

        /// <summary>
        /// Log the id for this process and whether it is running with Server GC.
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            Pid = Process.GetCurrentProcess().Id;
            IsServerGC = System.Runtime.GCSettings.IsServerGC;
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
        <Message Text=""[Work around Github issue #9667 with --interactive]Server ID is $(PID)"" Importance=""High"" />
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
            // mre must be declared before watcher so that it is disposed after watcher.
            // Reversing this order would allow late FileSystemWatcher callbacks to call
            // mre.Set() on a disposed ManualResetEvent, causing an ObjectDisposedException.
            using ManualResetEvent mre = new ManualResetEvent(false);
            using var watcher = new System.IO.FileSystemWatcher(dir!);
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
        public void ServerShouldStartWhenBuildIsInteractive()
        {
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path + " -interactive", out bool success, false, _output);
            int pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int pidOfServerProcess = ParseNumber(output, "Server ID is ");

            success.ShouldBeTrue();

            var serverProcess = Process.GetProcessById(pidOfServerProcess);

            serverProcess.HasExited.ShouldBeFalse();

            pidOfInitialProcess.ShouldNotBe(pidOfServerProcess, "We failed to start a server node when interactive is true.");
            bool serverIsDown = MSBuildClient.ShutdownServer(CancellationToken.None);
            serverIsDown.ShouldBeTrue();
        }

        [Fact]
        public void ServerStartsWhenMtPresentEvenWithoutEnvVar()
        {
            // Regression test for the "-mt implies MSBuild Server" routing decision
            // (investigation #9379, ShouldUseMSBuildServer / IsMultiThreadedRequested).
            // When MSBUILDUSESERVER is unset and the user passes -mt, the client should engage
            // the server automatically. Verified by running two builds back-to-back and asserting
            // the server process PID is the SAME for both — server reuse is the unique signature
            // of MSBuild server engagement (a non-server build would always get a fresh worker PID).
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            // Explicitly clear MSBUILDUSESERVER so we test the -mt-implies-server path.
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", null);

            // Make sure we start with no server running.
            MSBuildClient.ShutdownServer(CancellationToken.None);

            string output1 = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path + " -mt", out bool success1, false, _output);
            success1.ShouldBeTrue();
            int serverPid1 = ParseNumber(output1, "Server ID is ");

            string output2 = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path + " -mt", out bool success2, false, _output);
            success2.ShouldBeTrue();
            int serverPid2 = ParseNumber(output2, "Server ID is ");

            serverPid1.ShouldBe(serverPid2, "When -mt implies server, two consecutive builds should reuse the same server process. PIDs were " + serverPid1 + " and " + serverPid2 + ".");

            _env.WithTransientProcess(serverPid1);
            // Clean up the server we spun up.
            MSBuildClient.ShutdownServer(CancellationToken.None);
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
        <Message Text=""[Work around Github issue #9667 with --interactive]Server ID is $(PID)"" Importance=""High"" />
		<Message Text=""[Work around Github issue #9667 with --interactive]:MSBuildStartupDirectory:$(MSBuildStartupDirectory):"" Importance=""high"" />
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

#if NET
        /// <summary>
        /// Builds a project that reports, for the process that executes <see cref="ProcessIdTask"/>,
        /// its PID and whether it runs with Server GC. When <paramref name="useTaskHostFactory"/> is
        /// true the task is forced out-of-proc into a TaskHost, so its PID is the TaskHost's rather
        /// than the build node's.
        /// </summary>
        private static string GetServerGCProbeProjectContents(bool useTaskHostFactory)
        {
            string taskFactoryAttribute = useTaskHostFactory ? @" TaskFactory=""TaskHostFactory""" : string.Empty;
            return $@"
<Project>
<UsingTask TaskName=""ProcessIdTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}""{taskFactoryAttribute} />
    <Target Name='Probe'>
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
            <Output PropertyName=""SERVERGC"" TaskParameter=""IsServerGC"" />
        </ProcessIdTask>
        <Message Text=""[Work around GitHub issue #9667 with --interactive]TaskRanInPID=$(PID)"" Importance=""High"" />
        <Message Text=""TaskNodeServerGC=$(SERVERGC)"" Importance=""High"" />
    </Target>
</Project>";
        }

        /// <summary>
        /// Isolates a server-related test: a unique handshake salt guarantees a freshly launched server
        /// (so no leftover server from another test or local run is reused), and a clean GC environment
        /// ensures the server's Server GC comes from the launch injection rather than an ambient
        /// CI/user setting leaking into child nodes.
        /// </summary>
        private void PrepareIsolatedServerEnv(bool useServer = true)
        {
            // Child node launches (TaskHost, worker) need DOTNET_HOST_PATH to locate the runtime.
            RunnerUtilities.ApplyDotnetHostPathEnvironmentVariable(_env);
            _env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));
            _env.SetEnvironmentVariable("DOTNET_gcServer", null);
            _env.SetEnvironmentVariable("COMPlus_gcServer", null);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", useServer ? "1" : null);
        }

        /// <summary>
        /// The MSBuild server (build orchestrator) process must be launched with Server GC when the
        /// build is multithreaded (/mt) - that is when the server itself does the project work.
        /// </summary>
        [Fact]
        public void MultiThreadedServerProcessUsesServerGC()
        {
            if (Environment.ProcessorCount < 2)
            {
                Assert.Skip("Server GC can report as Workstation GC on single-processor machines.");
            }

            PrepareIsolatedServerEnv();
            TransientTestFile project = _env.CreateFile("serverGcProbe.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{project.Path} -mt", out bool success, false, _output);

            success.ShouldBeTrue();
            int clientPid = ParseNumber(output, "Process ID is ");
            int serverPid = ParseNumber(output, "TaskRanInPID=");
            _env.WithTransientProcess(serverPid);
            serverPid.ShouldNotBe(clientPid, "The build should run in the server node, not the entry process.");
            output.ShouldContain("TaskNodeServerGC=True", customMessage: "A multithreaded MSBuild server process should run with Server GC.");
        }

        /// <summary>
        /// Without /mt the server only orchestrates (project work happens in separate worker nodes),
        /// so the server process must keep the default Workstation GC.
        /// </summary>
        [Fact]
        public void NonMultiThreadedServerProcessDoesNotUseServerGC()
        {
            PrepareIsolatedServerEnv();
            TransientTestFile project = _env.CreateFile("serverGcProbeNoMt.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success, false, _output);

            success.ShouldBeTrue();
            int clientPid = ParseNumber(output, "Process ID is ");
            int serverPid = ParseNumber(output, "TaskRanInPID=");
            _env.WithTransientProcess(serverPid);
            serverPid.ShouldNotBe(clientPid, "The build should run in the server node, not the entry process.");
            output.ShouldContain("TaskNodeServerGC=False", customMessage: "A non-multithreaded MSBuild server process should keep the default Workstation GC.");
        }

        /// <summary>
        /// A TaskHost process must keep the default Workstation GC, even though a multithreaded server
        /// uses Server GC. Runs two /mt builds against the same (uniquely salted) server: one in-proc to
        /// capture the Server-GC server PID, then one that forces the task into a TaskHost.
        /// </summary>
        [Fact]
        public void TaskHostProcessDoesNotUseServerGC()
        {
            if (Environment.ProcessorCount < 2)
            {
                Assert.Skip("Server GC can report as Workstation GC on single-processor machines.");
            }

            PrepareIsolatedServerEnv();

            // First /mt build runs the task in-proc in the server node so we can capture the Server-GC server PID.
            TransientTestFile serverProbe = _env.CreateFile("serverProbe.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));
            string serverOutput = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{serverProbe.Path} -mt", out bool serverSuccess, false, _output);
            serverSuccess.ShouldBeTrue();
            int serverPid = ParseNumber(serverOutput, "TaskRanInPID=");
            _env.WithTransientProcess(serverPid);
            serverOutput.ShouldContain("TaskNodeServerGC=True", customMessage: "A multithreaded MSBuild server process should run with Server GC.");

            // Second /mt build (same server, reused via the shared handshake salt) forces the task out-of-proc.
            TransientTestFile taskHostProbe = _env.CreateFile("taskHostProbe.proj", GetServerGCProbeProjectContents(useTaskHostFactory: true));
            string taskHostOutput = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{taskHostProbe.Path} -mt", out bool taskHostSuccess, false, _output);
            taskHostSuccess.ShouldBeTrue();
            int clientPid = ParseNumber(taskHostOutput, "Process ID is ");
            int taskHostPid = ParseNumber(taskHostOutput, "TaskRanInPID=");
            _env.WithTransientProcess(taskHostPid);

            taskHostPid.ShouldNotBe(clientPid, "The task should run out-of-proc in a TaskHost, not the entry process.");
            taskHostPid.ShouldNotBe(serverPid, "The task should run in a TaskHost, not in the server node.");
            taskHostOutput.ShouldContain("TaskNodeServerGC=False", customMessage: "A TaskHost process must use Workstation GC even when the server uses Server GC.");
        }

        /// <summary>
        /// An out-of-proc worker node must keep the default Workstation GC.
        /// </summary>
        [Fact]
        public void WorkerNodeDoesNotUseServerGC()
        {
            PrepareIsolatedServerEnv(useServer: false);
            _env.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
            TransientTestFile project = _env.CreateFile("workerGcProbe.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{project.Path} /m:1", out bool success, false, _output);

            success.ShouldBeTrue();
            int clientPid = ParseNumber(output, "Process ID is ");
            int workerPid = ParseNumber(output, "TaskRanInPID=");
            _env.WithTransientProcess(workerPid);
            workerPid.ShouldNotBe(clientPid, "The build should run in an out-of-proc worker node, not the entry process.");
            output.ShouldContain("TaskNodeServerGC=False", customMessage: "A worker node must use the default Workstation GC.");
        }
#endif

        private int ParseNumber(string searchString, string toFind)
        {
            Regex regex = new(@$"{toFind}(\d+)");
            Match match = regex.Match(searchString);
            return int.Parse(match.Groups[1].Value);
        }
    }
}
