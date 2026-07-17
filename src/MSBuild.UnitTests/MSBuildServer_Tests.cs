// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.CommandLine;
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
        public void ServerSpawnAndReuseAreLoggedToBuildLog()
        {
            // Ensure a clean slate so the first build below deterministically spawns a new server node.
            MSBuildClient.ShutdownServer(CancellationToken.None);

            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            // First (cold) build: the server node is spawned for this build. The lifecycle message is
            // logged at low importance, so it only appears at diagnostic verbosity (and in a binary log).
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{project.Path} -verbosity:diagnostic", out bool success, false, _output);
            success.ShouldBeTrue();
            int serverPid = ParseNumber(output, "Server ID is ");
            _env.WithTransientProcess(serverPid);
            ParseNumber(output, "Process ID is ").ShouldNotBe(serverPid, "The build should have run on a separate server node.");

            string spawnedMessage = GetServerStatusMessage("MSBuildServerNodeSpawned", serverPid);
            string reusedMessage = GetServerStatusMessage("MSBuildServerNodeReused", serverPid);

            output.ShouldContain(spawnedMessage);
            output.ShouldNotContain(reusedMessage);

            // Second (warm) build: the running server node is reused.
            output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{project.Path} -verbosity:diagnostic", out success, false, _output);
            success.ShouldBeTrue();
            ParseNumber(output, "Server ID is ").ShouldBe(serverPid, "The second build should reuse the same server node.");

            output.ShouldContain(reusedMessage);
            output.ShouldNotContain(spawnedMessage);
        }

        [Fact]
        public void ServerNotUsedReasonIsLoggedToBuildLog()
        {
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            // MSBuild Server is requested via the environment variable, but /nodereuse:false makes the
            // command line incompatible with the server, so the build falls back to running in-process.
            // The specific reason (node reuse disabled) must be recorded in the build log.
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{project.Path} /nodereuse:false -verbosity:diagnostic", out bool success, false, _output);
            success.ShouldBeTrue();
            ParseNumber(output, "Process ID is ").ShouldBe(ParseNumber(output, "Server ID is "), "The build should have run in-process, not on a server node.");

            string reason = GetServerStatusMessage("MSBuildServerReasonNodeReuseDisabled");
            string notUsedMessage = GetServerStatusMessage("MSBuildServerNotUsedForBuild", reason);

            output.ShouldContain(notUsedMessage);
        }

        [Fact]
        public void ServerShortLivedForMultithreadedWhenNodeReuseOff()
        {
            // Ensure a clean slate so the build below deterministically spawns a new (short-lived) server node.
            MSBuildClient.ShutdownServer(CancellationToken.None);

            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            // A multithreaded (/mt) build with node reuse off still uses the server (for Server GC), but as a
            // short-lived node that tears itself down after this build. The lifecycle message must say so.
            _env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "1");
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{project.Path} /nodereuse:false -verbosity:diagnostic", out bool success, false, _output);
            success.ShouldBeTrue();
            int serverPid = ParseNumber(output, "Server ID is ");
            _env.WithTransientProcess(serverPid);
            ParseNumber(output, "Process ID is ").ShouldNotBe(serverPid, "The build should have run on a separate (short-lived) server node.");

            output.ShouldContain(GetServerStatusMessage("MSBuildServerNodeSpawnedShortLived", serverPid));
            // The ordinary (resident) spawn message must not appear for a short-lived server.
            output.ShouldNotContain(GetServerStatusMessage("MSBuildServerNodeSpawned", serverPid));
        }

        [Fact]
        public void ServerLifecycleMessagesAreAbsentForPlainBuild()
        {
            TransientTestFile project = _env.CreateFile("testProject.proj", printPidContents);

            // MSBuild Server is not requested for this invocation, so none of the server lifecycle messages
            // should be logged even at diagnostic verbosity (and the build runs in-process).
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "");
            string output = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{project.Path} -verbosity:diagnostic", out bool success, false, _output);
            success.ShouldBeTrue();
            ParseNumber(output, "Process ID is ").ShouldBe(ParseNumber(output, "Server ID is "), "The build should have run in-process, not on a server node.");

            output.ShouldNotContain(GetServerStatusMessage("MSBuildServerNodeSpawned", ParseNumber(output, "Server ID is ")));
            output.ShouldNotContain(GetServerStatusMessage("MSBuildServerNodeReused", ParseNumber(output, "Server ID is ")));
            // The not-used template (with its substituted reason) must not appear because the server was never requested.
            output.ShouldNotContain(GetServerStatusMessage("MSBuildServerNotUsedForBuild", GetServerStatusMessage("MSBuildServerReasonNodeReuseDisabled")));
        }

        /// <summary>
        /// Regression test for dotnet/msbuild#13940. With the server enabled, TerminalLogger
        /// auto-detection runs in the server node. It must honor the client's transmitted console
        /// configuration rather than the node's own (redirected) stdout. Here the build output is
        /// captured/redirected, so '-tl:auto' must fall back to the console logger and emit no
        /// TerminalLogger ANSI escape sequences.
        /// </summary>
        [Fact]
        public void TerminalLoggerAutoIsNotSelectedWhenServerOutputIsRedirected()
        {
            TransientTestFile project = _env.CreateFile("tlAutoProject.proj", printPidContents);
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            string output = RunnerUtilities.ExecMSBuild(
                BuildEnvironmentHelper.Instance.CurrentMSBuildExePath,
                $"{project.Path} -tl:auto",
                out bool success,
                false,
                _output);

            success.ShouldBeTrue();

            int pidOfInitialProcess = ParseNumber(output, "Process ID is ");
            int pidOfServerProcess = ParseNumber(output, "Server ID is ");
            _env.WithTransientProcess(pidOfServerProcess);
            pidOfInitialProcess.ShouldNotBe(pidOfServerProcess, "The build should have run on a separate server node.");

            // The output is redirected here, so TerminalLogger must not be auto-selected; its
            // characteristic ANSI cursor-hide sequence must not appear in the captured output.
            output.ShouldNotContain("\x1b[?25l");
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

        [ActiveIssue("https://github.com/dotnet/msbuild/issues/14195", TestPlatforms.Windows)]
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
            // Explicitly clear MSBUILDUSESERVER so we test the -mt-implies-server path, and isolate this
            // test's server with a unique handshake salt so it can't reuse/shut down an unrelated server.
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", null);
            _env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));

            // Make sure we start with no server running.
            MSBuildClient.ShutdownServer(CancellationToken.None);

            string output1 = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path + " -mt", out bool success1, false, _output);
            success1.ShouldBeTrue();
            int serverPid1 = ParseNumber(output1, "Server ID is ");
            // Register cleanup before any assertion so the server does not leak if an assertion throws.
            _env.WithTransientProcess(serverPid1);

            string output2 = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path + " -mt", out bool success2, false, _output);
            success2.ShouldBeTrue();
            int serverPid2 = ParseNumber(output2, "Server ID is ");

            serverPid1.ShouldBe(serverPid2, "When -mt implies server, two consecutive builds should reuse the same server process. PIDs were " + serverPid1 + " and " + serverPid2 + ".");

            // Clean up the server we spun up.
            MSBuildClient.ShutdownServer(CancellationToken.None);
        }

        [Fact]
        public void ServerStartsWhenMtInResponseFileEvenWithoutEnvVar()
        {
            // Regression test for rainersigwald's review concern (#13758): -mt enabled via a response file
            // (here a project Directory.Build.rsp) - not on the command line - must still implicitly engage the
            // server. This is the expected dogfooding mechanism, so the authoritative, response-file-aware parse
            // drives the decision. Verified the same way as ServerStartsWhenMtPresentEvenWithoutEnvVar: two builds
            // back-to-back reuse the SAME server PID, the unique signature of server engagement.
            TransientTestFolder folder = _env.CreateFolder();
            TransientTestFile project = _env.CreateFile(folder, "testProject.proj", printPidContents);
            // -mt comes ONLY from the response file; it is NOT passed on the command line below.
            _env.CreateFile(folder, "Directory.Build.rsp", "-mt");

            // Explicitly clear MSBUILDUSESERVER so we test the implicit path, and isolate this test's server with
            // a unique handshake salt so it can't reuse/shut down an unrelated server.
            _env.SetEnvironmentVariable("MSBUILDUSESERVER", null);
            _env.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", Guid.NewGuid().ToString("N"));

            // Make sure we start with no server running.
            MSBuildClient.ShutdownServer(CancellationToken.None);

            string output1 = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success1, false, _output);
            success1.ShouldBeTrue();
            int serverPid1 = ParseNumber(output1, "Server ID is ");
            // Register cleanup before any assertion so the server does not leak if an assertion throws.
            _env.WithTransientProcess(serverPid1);

            string output2 = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, project.Path, out bool success2, false, _output);
            success2.ShouldBeTrue();
            int serverPid2 = ParseNumber(output2, "Server ID is ");

            serverPid1.ShouldBe(serverPid2, "When -mt from a response file implies server, two consecutive builds should reuse the same server process. PIDs were " + serverPid1 + " and " + serverPid2 + ".");

            // Clean up the server we spun up.
            MSBuildClient.ShutdownServer(CancellationToken.None);
        }

#if NET
        /// <summary>
        /// Disabling node reuse (e.g. <c>-nr:false</c>, as <c>dotnet restore</c> does) must NOT prevent a
        /// multithreaded (/mt) build from using the server. Instead of skipping the server, the no-reuse intent is
        /// honored by shutting the server down after the build. This test verifies both halves: the build runs in a
        /// separate server process, and that process does not survive the build (so a subsequent build gets a fresh server).
        /// </summary>
        [Fact]
        public void MultiThreadedServerIsUsedButShutDownWhenNodeReuseDisabled()
        {
            // Clear MSBUILDUSESERVER so we exercise the -mt-implies-server path, and isolate this test's server
            // with a unique handshake salt and a clean environment.
            PrepareIsolatedServerEnv(useServer: false);
            TransientTestFile project = _env.CreateFile("mtNoReuseProbe.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));

            // Make sure we start with no server running.
            MSBuildClient.ShutdownServer(CancellationToken.None);

            try
            {
                // -mt forces the server on even though node reuse is disabled.
                string output1 = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{project.Path} -mt -nr:false", out bool success1, false, _output);
                success1.ShouldBeTrue();
                int clientPid1 = ParseNumber(output1, "Process ID is ");
                int serverPid1 = ParseNumber(output1, "TaskRanInPID=");
                // Register cleanup before any assertion so the server does not leak if an assertion throws.
                _env.WithTransientProcess(serverPid1);

                // The build ran in a separate server process: proof the server was engaged despite -nr:false.
                serverPid1.ShouldNotBe(clientPid1, "Even with node reuse disabled, -mt must run the build in the server node, not the entry process.");

                // Because node reuse is disabled, the server must not persist past the build: its process should exit.
                WaitForProcessExit(serverPid1).ShouldBeTrue($"Server process {serverPid1} should have been shut down after the build when node reuse is disabled.");

                // A second build cannot reuse the (now gone) server, so it must launch a fresh server process.
                string output2 = RunnerUtilities.ExecMSBuild(BuildEnvironmentHelper.Instance.CurrentMSBuildExePath, $"{project.Path} -mt -nr:false", out bool success2, false, _output);
                success2.ShouldBeTrue();
                int serverPid2 = ParseNumber(output2, "TaskRanInPID=");
                _env.WithTransientProcess(serverPid2);
                serverPid2.ShouldNotBe(serverPid1, "With node reuse disabled, each -mt build should get a fresh, non-persistent server process.");
            }
            finally
            {
                // Ensure any server we spun up is torn down even if an assertion above fails.
                MSBuildClient.ShutdownServer(CancellationToken.None);
            }
        }

        /// <summary>
        /// Waits up to <paramref name="timeoutMs"/> for the process with the given PID to exit. Returns true if
        /// the process exited (or was already gone), false if it was still running when the timeout elapsed.
        /// </summary>
        private static bool WaitForProcessExit(int pid, int timeoutMs = 10000)
        {
            try
            {
                using Process process = Process.GetProcessById(pid);
                return process.WaitForExit(timeoutMs);
            }
            catch (ArgumentException)
            {
                // No process with that PID is running - it has already exited.
                return true;
            }
        }
#endif

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

        /// <summary>
        /// Resolves an MSBuild Server status message from the MSBuild executable's own resources so the
        /// assertions stay locale-independent (the child build process and this resource lookup share the
        /// same culture and resource set).
        /// </summary>
        private static string GetServerStatusMessage(string resourceName, params object[] args)
        {
            ResourceManager resourceManager = new("MSBuild.Strings", typeof(MSBuildApp).Assembly);
            string format = resourceManager.GetString(resourceName, CultureInfo.CurrentUICulture)
                ?? throw new InvalidOperationException($"Resource '{resourceName}' was not found in the MSBuild executable resources.");
            return args.Length == 0 ? format : string.Format(CultureInfo.CurrentCulture, format, args);
        }
    }
}
