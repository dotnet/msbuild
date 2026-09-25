// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.CommandLine;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Server;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Path = System.IO.Path;

namespace Microsoft.Build.Engine.UnitTests
{
    /// <summary>
    /// Blocks on the <see cref="NodeScenario.Gate"/> named <see cref="Name"/>, so a test can hold a build open in
    /// whichever process runs this task for exactly as long as it needs to.
    /// </summary>
    [MSBuildMultiThreadableTask]
    public class ScenarioGateTask : Microsoft.Build.Utilities.Task
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        public override bool Execute()
        {
            NodeScenarioGate.Enter(Name);
            return true;
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

    public class SidecarProcessIdTask : Microsoft.Build.Utilities.Task
    {
        /// <summary>
        /// Optional. When set, the task reports the value this process sees for that environment
        /// variable, so a test can tell whether a reused process picked up the current build's
        /// environment. Callers that only need the process id leave it unset.
        /// </summary>
        public string? EnvironmentVariableName { get; set; }

        [Output]
        public int Pid { get; set; }

        [Output]
        public string? EnvironmentVariableValue { get; set; }

        public override bool Execute()
        {
            Pid = Process.GetCurrentProcess().Id;

            if (!string.IsNullOrEmpty(EnvironmentVariableName))
            {
                EnvironmentVariableValue = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            }

            return true;
        }
    }

    /// <summary>
    /// MSBuild server behaviour, observed through the node lifecycle journal of a <see cref="NodeScenario"/>. The
    /// scenario gives every test its own handshake salt, so no test can reuse or shut down another test's server, and
    /// each test shuts down the servers it started, so none leaks into the next test.
    /// </summary>
    public class MSBuildServer_Tests
    {
        private const string ServerBusyGate = "server-busy";

        private readonly ITestOutputHelper _output;
        private static readonly string printPidContents = @$"
<Project>
<UsingTask TaskName=""ProcessIdTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <Target Name='AccessPID'>
        <ProcessIdTask>
            <Output PropertyName=""PID"" TaskParameter=""Pid"" />
        </ProcessIdTask>
        <Message Text=""[Work around Github issue #9667 with --interactive]Server ID is $(PID)"" Importance=""High"" />
    </Target>
</Project>";
        private static readonly string gatedProjectContents = @$"
<Project>
<UsingTask TaskName=""ScenarioGateTask"" AssemblyFile=""{Assembly.GetExecutingAssembly().Location}"" />
    <Target Name='Wait'>
        <ScenarioGateTask Name=""{ServerBusyGate}"" />
    </Target>
</Project>";

        public MSBuildServer_Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string MSBuildExePath => BuildEnvironmentHelper.Instance.CurrentMSBuildExePath;

        [NodeScenarioFact]
        public void ServersAreIsolatedByResolvedChangeWave()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            var project = scenario.Environment.CreateFile("strict-server.proj", $"""
                <Project>
                  <UsingTask TaskName="ProcessIdTask" AssemblyFile="{Assembly.GetExecutingAssembly().Location}" />
                  <UsingTask TaskName="StrictModeProbeTask" AssemblyFile="{Assembly.GetExecutingAssembly().Location}" />
                  <Target Name="Build">
                    <ProcessIdTask>
                      <Output PropertyName="PID" TaskParameter="Pid" />
                    </ProcessIdTask>
                    <Message Text="Server ID is $(PID)" Importance="high" />
                    <StrictModeProbeTask Behavior="ChangeCurrentDirectory" />
                  </Target>
                </Project>
                """);
            Dictionary<int, bool> strictModeByServerPid = [];
            // Unset and 999.999 resolve identically, so the last request may reuse the first server.
            (string? DisabledWave, bool StrictModeEnabled)[] requests =
            [
                (null, true),
                ("18.12", false),
                ("999.999", true),
            ];
            foreach ((string? disabledWave, bool strictModeEnabled) in requests)
            {
                scenario.Environment.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disabledWave);
                ServerBuild build = Build(scenario, $"\"{project.Path}\" -m:1 -mt -nr:true", expectSuccess: !strictModeEnabled);
                build.ShouldRunOnServer();
                ParseNumber(build.Output, "Server ID is ").ShouldBe(build.ServerProcessId);
                if (strictModeByServerPid.TryGetValue(build.ServerProcessId, out bool previousStrictModeEnabled))
                {
                    strictModeEnabled.ShouldBe(previousStrictModeEnabled,
                        "Requests with different resolved change waves must not reuse the same server process.");
                }
                else
                {
                    strictModeByServerPid.Add(build.ServerProcessId, strictModeEnabled);
                }

                build.Output.Contains("MSB4286").ShouldBe(strictModeEnabled, build.Output);
            }

            // The change wave is part of the server's identity, so each server has to be shut down with its own.
            scenario.ShutdownNodes(() =>
            {
                foreach ((string? disabledWave, _) in requests)
                {
                    scenario.Environment.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disabledWave);
                    ChangeWaves.ResetStateForTests();
                    MSBuildClient.ShutdownServer(CancellationToken.None);
                }

                scenario.Environment.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", null);
                ChangeWaves.ResetStateForTests();
            });
        }

        [NodeScenarioFact]
        public void MSBuildServerTest()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            // The server crashes as soon as the gated build below holds it busy.
            scenario.Fault(NodeJournalEvent.GateEntered, NodeFaultAction.Crash, NodeJournalKind.Server);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);

            ServerBuild first = Build(scenario, project.Path);
            first.ShouldRunOnServer();
            first.ServerReuseDecision.ShouldBe("new");

            ServerBuild second = Build(scenario, project.Path);
            second.ShouldRunOnServer();
            second.ClientProcessId.ShouldNotBe(first.ClientProcessId, "Process started by two MSBuild executions should be different.");
            second.ServerProcessId.ShouldBe(first.ServerProcessId, "Node used by both the first and second build should be the same.");
            second.ServerReuseDecision.ShouldBe("reused");

            // The server dies in the middle of a build.
            TransientTestFile gatedProject = scenario.Environment.CreateFile("napProject.proj", gatedProjectContents);
            scenario.Gate(ServerBusyGate);
            scenario.RunMSBuild(MSBuildExePath, gatedProject.Path);
            scenario.Await(NodeJournalEvent.FaultInjected, processId: first.ServerProcessId);

            // A new build can still succeed, on a new server.
            ServerBuild third = Build(scenario, project.Path);
            third.ShouldRunOnServer();
            third.ClientProcessId.ShouldNotBe(first.ClientProcessId, "Process started by two MSBuild executions should be different.");
            third.ServerProcessId.ShouldNotBe(first.ServerProcessId, "The build after the crash must not use the crashed server.");
            third.ServerReuseDecision.ShouldBe("new");

            scenario.ShutdownNodes();
        }

        [NodeScenarioFact]
        public void ServerSpawnAndReuseAreLoggedToBuildLog()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            // First (cold) build: the server node is spawned for this build. The lifecycle message is
            // logged at low importance, so it only appears at diagnostic verbosity (and in a binary log).
            ServerBuild first = Build(scenario, $"{project.Path} -verbosity:diagnostic");
            first.ShouldRunOnServer();
            first.ServerReuseDecision.ShouldBe("new");

            string spawnedMessage = GetServerStatusMessage("MSBuildServerNodeSpawned", first.ServerProcessId);
            string reusedMessage = GetServerStatusMessage("MSBuildServerNodeReused", first.ServerProcessId);

            first.Output.ShouldContain(spawnedMessage);
            first.Output.ShouldNotContain(reusedMessage);

            // Second (warm) build: the running server node is reused.
            ServerBuild second = Build(scenario, $"{project.Path} -verbosity:diagnostic");
            second.ServerProcessId.ShouldBe(first.ServerProcessId, "The second build should reuse the same server node.");
            second.ServerReuseDecision.ShouldBe("reused");

            second.Output.ShouldContain(reusedMessage);
            second.Output.ShouldNotContain(spawnedMessage);

            scenario.ShutdownNodes();
        }

        [NodeScenarioFact]
        public void DeferredLoggerMessagesDoNotAccumulateAcrossServerBuilds()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            scenario.Environment.SetEnvironmentVariable("CI", "1");

            string arguments = $"{project.Path} -terminalLogger:auto -verbosity:diagnostic";
            ServerBuild first = Build(scenario, arguments);
            first.ShouldRunOnServer();

            string terminalLoggerMessage = ResourceUtilities.GetResourceString("TerminalLoggerNotUsedAutomated");
            int initialMessageCount = Regex.Matches(first.Output, Regex.Escape(terminalLoggerMessage)).Count;
            initialMessageCount.ShouldBeGreaterThan(0);

            ServerBuild second = Build(scenario, arguments);
            second.ServerProcessId.ShouldBe(first.ServerProcessId);
            Regex.Matches(second.Output, Regex.Escape(terminalLoggerMessage)).Count.ShouldBe(initialMessageCount);

            scenario.ShutdownNodes();
        }

        [NodeScenarioFact]
        public void ProcessPriorityDoesNotLeakAcrossServerBuilds()
        {
            if (!NativeMethodsShared.IsWindows)
            {
                Assert.Skip("Process priority classes are Windows-only.");
            }

            using NodeScenario scenario = NodeScenario.Create(_output);
            ProcessPriorityClass originalPriority = Process.GetCurrentProcess().PriorityClass;

            string projectContents = printPidContents.Replace(
                "</Target>",
                "<Message Text=\"Server priority is '$([System.Diagnostics.Process]::GetCurrentProcess().PriorityClass)'\" Importance=\"High\" /></Target>");
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", projectContents);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            // System.Diagnostics.Process is not allowlisted for .NET Framework property functions.
            scenario.Environment.SetEnvironmentVariable("MSBUILDENABLEALLPROPERTYFUNCTIONS", "1");

            ServerBuild low = Build(scenario, $"{project.Path} -low");
            low.ShouldRunOnServer();
            low.Output.ShouldContain("Server priority is 'BelowNormal'");

            // Build waits until the server released its busy mutex, so this build cannot fall back in-process (#15093).
            ServerBuild normal = Build(scenario, project.Path);
            normal.ServerProcessId.ShouldBe(low.ServerProcessId);
            normal.Output.ShouldContain($"Server priority is '{originalPriority}'");

            scenario.ShutdownNodes();
        }

        [NodeScenarioFact]
        public void ServerNotUsedReasonIsLoggedToBuildLog()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            // MSBuild Server is requested via the environment variable, but /nodereuse:false makes the
            // command line incompatible with the server, so the build falls back to running in-process.
            // The specific reason (node reuse disabled) must be recorded in the build log.
            ServerBuild build = Build(scenario, $"{project.Path} /nodereuse:false -verbosity:diagnostic");
            build.ShouldRunInProc(scenario);

            string reason = GetServerStatusMessage("MSBuildServerReasonNodeReuseDisabled");
            string notUsedMessage = GetServerStatusMessage("MSBuildServerNotUsedForBuild", reason);

            build.Output.ShouldContain(notUsedMessage);
        }

        [NodeScenarioFact]
        public void ServerShortLivedForMultithreadedWhenNodeReuseOff()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            // A multithreaded (/mt) build with node reuse off still uses the server (for Server GC), but as a
            // short-lived node that tears itself down after this build. The lifecycle message must say so.
            scenario.Environment.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "1");
            ServerBuild build = Build(scenario, $"{project.Path} /nodereuse:false -verbosity:diagnostic");
            build.ShouldRunOnServer();

            build.Output.ShouldContain(GetServerStatusMessage("MSBuildServerNodeSpawnedShortLived", build.ServerProcessId));
            // The ordinary (resident) spawn message must not appear for a short-lived server.
            build.Output.ShouldNotContain(GetServerStatusMessage("MSBuildServerNodeSpawned", build.ServerProcessId));
            scenario.Await(NodeJournalEvent.Exited, processId: build.ServerProcessId);
        }

        [NodeScenarioFact]
        public void ServerLifecycleMessagesAreAbsentForPlainBuild()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);

            // MSBuild Server is not requested for this invocation, so none of the server lifecycle messages
            // should be logged even at diagnostic verbosity (and the build runs in-process).
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "");
            ServerBuild build = Build(scenario, $"{project.Path} -verbosity:diagnostic");
            build.ShouldRunInProc(scenario);

            build.Output.ShouldNotContain(GetServerStatusMessage("MSBuildServerNodeSpawned", build.ClientProcessId));
            build.Output.ShouldNotContain(GetServerStatusMessage("MSBuildServerNodeReused", build.ClientProcessId));
            // The not-used template (with its substituted reason) must not appear because the server was never requested.
            build.Output.ShouldNotContain(GetServerStatusMessage("MSBuildServerNotUsedForBuild", GetServerStatusMessage("MSBuildServerReasonNodeReuseDisabled")));
        }

        /// <summary>
        /// Regression test for dotnet/msbuild#13940. With the server enabled, TerminalLogger
        /// auto-detection runs in the server node. It must honor the client's transmitted console
        /// configuration rather than the node's own (redirected) stdout. Here the build output is
        /// captured/redirected, so '-tl:auto' must fall back to the console logger and emit no
        /// TerminalLogger ANSI escape sequences.
        /// </summary>
        [NodeScenarioFact]
        public void TerminalLoggerAutoIsNotSelectedWhenServerOutputIsRedirected()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("tlAutoProject.proj", printPidContents);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            ServerBuild build = Build(scenario, $"{project.Path} -tl:auto");
            build.ShouldRunOnServer();

            // The output is redirected here, so TerminalLogger must not be auto-selected; its
            // characteristic ANSI cursor-hide sequence must not appear in the captured output.
            build.Output.ShouldNotContain("\x1b[?25l");

            scenario.ShutdownNodes();
        }

        [NodeScenarioFact]
        public void VerifyMixedLegacyBehavior()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            ServerBuild first = Build(scenario, project.Path);
            first.ShouldRunOnServer();

            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "");
            Build(scenario, project.Path).ShouldRunInProc(scenario);

            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            ServerBuild third = Build(scenario, project.Path);
            third.ShouldRunOnServer();
            third.ServerProcessId.ShouldBe(first.ServerProcessId, "Server node should be the same as from earlier.");

            scenario.ShutdownNodes();
        }

        [NodeScenarioFact]
        public void BuildsWhileBuildIsRunningOnServer()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);
            TransientTestFile gatedProject = scenario.Environment.CreateFile("napProject.proj", gatedProjectContents);

            // Start a server node.
            ServerBuild first = Build(scenario, project.Path);
            first.ShouldRunOnServer();

            // Hold the server busy with a build that waits on a gate.
            NodeScenario.GateHandle busy = scenario.Gate(ServerBusyGate);
            NodeScenarioRun busyRun = scenario.StartMSBuild(MSBuildExePath, gatedProject.Path);
            busy.AwaitEntered().ProcessId.ShouldBe(first.ServerProcessId, "The gated build should run on the server.");

            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "0");
            ServerBuild withoutServer = Build(scenario, project.Path);
            withoutServer.ShouldRunInProc(scenario);
            withoutServer.FellBackInProc.ShouldBeFalse("The server was not requested, so there is nothing to fall back from.");

            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            ServerBuild whileBusy = Build(scenario, project.Path);
            whileBusy.ShouldRunInProc(scenario);
            whileBusy.FellBackInProc.ShouldBeTrue("The server is occupied, so the build should fall back in-process.");

            busy.Release();
            busyRun.WaitForSuccess();
            scenario.ShutdownNodes();
        }

        [NodeScenarioTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void CanShutdownServerProcess(bool byBuildManager)
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);

            ServerBuild build = Build(scenario, project.Path);
            build.ShouldRunOnServer();
            scenario.Count(NodeScenario.Is(NodeJournalEvent.Exited, processId: build.ServerProcessId)).ShouldBe(0, "The server should outlive its build.");

            NodeJournalRecord shutdown = scenario.Marker("Shutdown");
            if (byBuildManager)
            {
                BuildManager.DefaultBuildManager.ShutdownAllNodes();
            }
            else
            {
                MSBuildClient.ShutdownServer(CancellationToken.None).ShouldBeTrue();
            }

            scenario.AssertOrder(
                r => r.Sequence == shutdown.Sequence, "shutdown requested",
                NodeScenario.Is(NodeJournalEvent.Exited, processId: build.ServerProcessId), "server exited");
        }

        [NodeScenarioFact]
        public void CanShutdownServerProcessWhenNotRunning()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            MSBuildClient.ShutdownServer(CancellationToken.None).ShouldBeTrue();
            scenario.Count(NodeScenario.Is(NodeJournalEvent.Launched)).ShouldBe(0);
        }

        [NodeScenarioFact]
        public void ServerShouldNotRunWhenNodeReuseEqualsFalse()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            Build(scenario, project.Path + " /nodereuse:false").ShouldRunInProc(scenario);
        }

        [NodeScenarioFact]
        public void ServerShouldStartWhenBuildIsInteractive()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            ServerBuild build = Build(scenario, project.Path + " -interactive");
            build.ShouldRunOnServer();
            scenario.Count(NodeScenario.Is(NodeJournalEvent.Exited, processId: build.ServerProcessId)).ShouldBe(0, "The server should outlive its build.");

            MSBuildClient.ShutdownServer(CancellationToken.None).ShouldBeTrue();
            scenario.Await(NodeJournalEvent.Exited, processId: build.ServerProcessId);
        }

        [NodeScenarioFact]
        public void ServerStartsWhenMtPresentEvenWithoutEnvVar()
        {
            // Regression test for the "-mt implies MSBuild Server" routing decision
            // (investigation #9379, ShouldUseMSBuildServer / IsMultiThreadedRequested).
            // When MSBUILDUSESERVER is unset and the user passes -mt, the client should engage
            // the server automatically, and the second build reuses it.
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", printPidContents);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", null);

            ServerBuild first = Build(scenario, project.Path + " -mt");
            first.ShouldRunOnServer();
            ServerBuild second = Build(scenario, project.Path + " -mt");
            second.ServerProcessId.ShouldBe(first.ServerProcessId, "When -mt implies server, two consecutive builds should reuse the same server process.");
            second.ServerReuseDecision.ShouldBe("reused");

            scenario.ShutdownNodes();
        }

        [NodeScenarioFact]
        public void ServerStartsWhenMtInResponseFileEvenWithoutEnvVar()
        {
            // Regression test for rainersigwald's review concern (#13758): -mt enabled via a response file
            // (here a project Directory.Build.rsp) - not on the command line - must still implicitly engage the
            // server. This is the expected dogfooding mechanism, so the authoritative, response-file-aware parse
            // drives the decision.
            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFolder folder = scenario.Environment.CreateFolder();
            TransientTestFile project = scenario.Environment.CreateFile(folder, "testProject.proj", printPidContents);
            // -mt comes ONLY from the response file; it is NOT passed on the command line below.
            scenario.Environment.CreateFile(folder, "Directory.Build.rsp", "-mt");
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", null);

            ServerBuild first = Build(scenario, project.Path);
            first.ShouldRunOnServer();
            ServerBuild second = Build(scenario, project.Path);
            second.ServerProcessId.ShouldBe(first.ServerProcessId, "When -mt from a response file implies server, two consecutive builds should reuse the same server process.");
            second.ServerReuseDecision.ShouldBe("reused");

            scenario.ShutdownNodes();
        }

        [NodeScenarioFact]
        public void PropertyMSBuildStartupDirectoryOnServer()
        {
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

            using NodeScenario scenario = NodeScenario.Create(_output);
            TransientTestFile project = scenario.Environment.CreateFile("testProject.proj", reportMSBuildStartupDirectoryProperty);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", "1");

            // Start on current working directory
            ServerBuild first = Build(scenario, $"/t:DisplayMessages {project.Path}");
            first.ShouldRunOnServer();
            first.Output.ShouldContain($@":MSBuildStartupDirectory:{Environment.CurrentDirectory}:");

            // Start on transient project directory
            scenario.Environment.SetCurrentDirectory(Path.GetDirectoryName(project.Path));
            ServerBuild second = Build(scenario, $"/t:DisplayMessages {project.Path}");
            second.ServerProcessId.ShouldBe(first.ServerProcessId);
            second.Output.ShouldContain($@":MSBuildStartupDirectory:{Environment.CurrentDirectory}:");

            scenario.ShutdownNodes();
        }

#if NET
        /// <summary>
        /// Disabling node reuse (e.g. <c>-nr:false</c>, as <c>dotnet restore</c> does) must NOT prevent a
        /// multithreaded (/mt) build from using the server. Instead of skipping the server, the no-reuse intent is
        /// honored by shutting the server down after the build. This test verifies both halves: the build runs in a
        /// separate server process, and that process does not survive the build (so a subsequent build gets a fresh server).
        /// </summary>
        [NodeScenarioFact]
        public void MultiThreadedServerIsUsedButShutDownWhenNodeReuseDisabled()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            PrepareIsolatedServerEnv(scenario, useServer: false);
            TransientTestFile project = scenario.Environment.CreateFile("mtNoReuseProbe.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));

            // -mt forces the server on even though node reuse is disabled.
            ServerBuild first = Build(scenario, $"{project.Path} -mt -nr:false");
            first.ShouldRunOnServer();
            ParseNumber(first.Output, "TaskRanInPID=").ShouldBe(first.ServerProcessId);

            // Because node reuse is disabled, the server must not persist past the build.
            scenario.Await(NodeJournalEvent.Exited, processId: first.ServerProcessId);

            // A second build cannot reuse the (now gone) server, so it must launch a fresh server process.
            ServerBuild second = Build(scenario, $"{project.Path} -mt -nr:false");
            second.ShouldRunOnServer();
            second.ServerProcessId.ShouldNotBe(first.ServerProcessId, "With node reuse disabled, each -mt build should get a fresh, non-persistent server process.");
            scenario.Await(NodeJournalEvent.Exited, processId: second.ServerProcessId);
        }

        [NodeScenarioFact]
        public void ServerOwnsReusableSidecarsUntilShutdown()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            PrepareIsolatedServerEnv(scenario, useServer: false);

            const string environmentVariableName = "MSBUILD_SERVER_OWNED_SIDECAR_TEST";
            scenario.Environment.SetEnvironmentVariable(environmentVariableName, "first-build");
            TransientTestFile project = scenario.Environment.CreateFile(
                "serverOwnedSidecar.proj",
                $"""
                <Project>
                    <UsingTask TaskName="ProcessIdTask" AssemblyFile="{Assembly.GetExecutingAssembly().Location}" />
                    <UsingTask TaskName="SidecarProcessIdTask" AssemblyFile="{Assembly.GetExecutingAssembly().Location}" />
                    <Target Name="Probe">
                        <ProcessIdTask>
                            <Output PropertyName="SERVER_PID" TaskParameter="Pid" />
                        </ProcessIdTask>
                        <SidecarProcessIdTask EnvironmentVariableName="{environmentVariableName}">
                            <Output PropertyName="SIDECAR_PID" TaskParameter="Pid" />
                            <Output PropertyName="SIDECAR_ENVIRONMENT" TaskParameter="EnvironmentVariableValue" />
                        </SidecarProcessIdTask>
                        <Message Text="Server ID is $(SERVER_PID)" Importance="High" />
                        <Message Text="Sidecar ID is $(SIDECAR_PID)" Importance="High" />
                        <Message Text="Sidecar environment is $(SIDECAR_ENVIRONMENT)" Importance="High" />
                    </Target>
                </Project>
                """);

            ServerBuild first = Build(scenario, $"{project.Path} -mt -nodeReuse:true", bootstrapped: true);
            first.ShouldRunOnServer();
            first.Output.ShouldContain("Sidecar environment is first-build");

            scenario.Environment.SetEnvironmentVariable(environmentVariableName, "second-build");
            ServerBuild second = Build(scenario, $"{project.Path} -mt -nodeReuse:true", bootstrapped: true);
            second.ServerProcessId.ShouldBe(first.ServerProcessId);
            second.Output.ShouldContain("Sidecar environment is second-build");

            // Every TaskHost the server launched. Whether the second build reused the first one depends on how node
            // reuse is configured, so assert about all of them rather than about their identity.
            int[] sidecars = [.. scenario.Records
                .Where(r => r.Event == NodeJournalEvent.Launched && r.Kind == NodeJournalKind.TaskHost && r.ProcessId == first.ServerProcessId)
                .Select(r => r.SubjectProcessId)];
            sidecars.ShouldNotBeEmpty("The server should have launched a sidecar TaskHost.");
            sidecars.ShouldContain(ParseNumber(first.Output, "Sidecar ID is "));
            sidecars.ShouldContain(ParseNumber(second.Output, "Sidecar ID is "));

            ShutdownBootstrapServer();
            scenario.Await(NodeJournalEvent.Exited, processId: first.ServerProcessId);

            // The guarantee under test: no TaskHost the server used may survive it.
            foreach (int sidecar in sidecars)
            {
                scenario.Await(NodeJournalEvent.Exited, processId: sidecar);
            }
        }

        /// <summary>
        /// Runs <c>build-server shutdown</c> against the bootstrap installation. The in-process
        /// <see cref="MSBuildClient.ShutdownServer"/> cannot be used here: it derives its handshake
        /// from the currently running MSBuild, which is a different installation than the bootstrap
        /// that served these builds, so it would not find that server.
        /// </summary>
        private void ShutdownBootstrapServer()
            => RunnerUtilities.RunProcessAndGetOutput(
                RunnerUtilities.BootstrapDotnetHostPath,
                "build-server shutdown --msbuild",
                out _,
                outputHelper: _output,
                environmentVariables: RunnerUtilities.GetBootstrapMSBuildEnvironmentVariables());

        [NodeScenarioTheory]
        [InlineData(null)]
        [InlineData("18.12")]
        public void SidecarWithoutServerHonorsOwnershipChangeWave(string? disabledWave)
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            PrepareIsolatedServerEnv(scenario, useServer: false);
            scenario.Environment.SetEnvironmentVariable(Traits.UseMSBuildServerEnvVarName, "0");
            scenario.Environment.SetEnvironmentVariable("MSBUILDDISABLEFEATURESFROMVERSION", disabledWave);

            TransientTestFile project = scenario.Environment.CreateFile(
                "unownedSidecar.proj",
                $"""
                <Project>
                    <UsingTask TaskName="SidecarProcessIdTask" AssemblyFile="{Assembly.GetExecutingAssembly().Location}" />
                    <Target Name="Probe">
                        <SidecarProcessIdTask EnvironmentVariableName="PATH">
                            <Output PropertyName="SIDECAR_PID" TaskParameter="Pid" />
                        </SidecarProcessIdTask>
                        <Message Text="Sidecar ID is $(SIDECAR_PID)" Importance="High" />
                    </Target>
                </Project>
                """);

            // A pooled sidecar starts its node loop again once its launcher is gone. Crashing it right there both
            // proves it was pooled and ends it, since nothing in this test can reach it to shut it down.
            scenario.Fault(NodeJournalEvent.NodeStarted, NodeFaultAction.Crash, NodeJournalKind.TaskHost, occurrence: 2);

            ServerBuild build = Build(scenario, $"{project.Path} -mt -nodeReuse:true", bootstrapped: true);
            build.ShouldRunInProc(scenario);
            int sidecarPid = ParseNumber(build.Output, "Sidecar ID is ");
            scenario.Await(NodeScenario.Is(NodeJournalEvent.Launched, NodeJournalKind.TaskHost, processId: build.ClientProcessId), "the client launched the sidecar")
                .SubjectProcessId.ShouldBe(sidecarPid);
            // Ownership ends the sidecar when its launcher exits. Opting out preserves pooling.
            if (disabledWave is null)
            {
                scenario.Await(NodeJournalEvent.Exited, processId: sidecarPid);
                scenario.AssertNever(NodeScenario.Is(NodeJournalEvent.FaultInjected, processId: sidecarPid), "the owned sidecar returns to the pool");
            }
            else
            {
                scenario.Await(NodeJournalEvent.FaultInjected, processId: sidecarPid);
                scenario.AssertNever(NodeScenario.Is(NodeJournalEvent.Exited, processId: sidecarPid), "the pooled sidecar exits with its launcher");
            }
        }

        /// <summary>
        /// A build that asks its server to shut down afterwards must not be able to reach the resident
        /// server and shut that down instead. Before transient servers had their own identity, both
        /// resolved to the same pipe and mutex names, so <c>-mt -nodeReuse:false</c> killed the
        /// resident server every time.
        /// </summary>
        [NodeScenarioFact]
        public void TransientBuildDoesNotShutDownResidentServer()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            PrepareIsolatedServerEnv(scenario, useServer: false);
            TransientTestFile project = scenario.Environment.CreateFile("transientVsResident.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));

            ServerBuild resident = Build(scenario, $"{project.Path} -mt -nodeReuse:true", bootstrapped: true);
            resident.ShouldRunOnServer();

            ServerBuild transient = Build(scenario, $"{project.Path} -mt -nodeReuse:false", bootstrapped: true);
            transient.ShouldRunOnServer();
            transient.ServerProcessId.ShouldNotBe(resident.ServerProcessId, "A transient build must run in its own server rather than borrowing the resident one.");
            NodeJournalRecord transientExited = scenario.Await(NodeJournalEvent.Exited, processId: transient.ServerProcessId);

            scenario.AssertNever(NodeScenario.Is(NodeJournalEvent.Exited, processId: resident.ServerProcessId), "the resident server exits", transientExited);
            scenario.ShutdownNodes(ShutdownBootstrapServer);
        }

        /// <summary>
        /// Each transient build gets a server of its own. A single shared transient identity would make
        /// these contend: the second build would find the first's server holding the running mutex and
        /// fall back in-process, silently losing the server that <c>-mt</c> asked for.
        /// </summary>
        [NodeScenarioFact]
        public void ConcurrentTransientBuildsEachGetTheirOwnServer()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            PrepareIsolatedServerEnv(scenario, useServer: false);

            // Both builds wait on the same gate, so they genuinely overlap: the second one starts while the first
            // one's server is busy.
            TransientTestFile project = scenario.Environment.CreateFile("concurrentTransient.proj", gatedProjectContents);
            NodeScenario.GateHandle gate = scenario.Gate(ServerBusyGate);

            string arguments = $"{project.Path} -mt -nodeReuse:false";
            NodeScenarioRun first = scenario.StartBootstrapped(arguments);
            NodeJournalRecord firstEntered = gate.AwaitEntered();
            NodeScenarioRun second = scenario.StartBootstrapped(arguments);
            NodeJournalRecord secondEntered = scenario.Await(
                r => r.Event == NodeJournalEvent.GateEntered && r.Detail == ServerBusyGate && r.ProcessId != firstEntered.ProcessId,
                "the second build reached the gate");
            gate.Release();
            first.WaitForSuccess();
            second.WaitForSuccess();

            // Falling back in-process is how contention shows up.
            scenario.Count(NodeScenario.Is(NodeJournalEvent.ServerBusyFallback)).ShouldBe(0, "Neither build may fall back in-process.");
            firstEntered.Role.ShouldBe(NodeJournalKind.Server, "The first build must run in a server, not in-process.");
            secondEntered.Role.ShouldBe(NodeJournalKind.Server, "The second build must run in a server, not in-process.");

            scenario.Await(NodeJournalEvent.Exited, processId: firstEntered.ProcessId);
            scenario.Await(NodeJournalEvent.Exited, processId: secondEntered.ProcessId);
        }

        /// <summary>
        /// A transient build with node reuse disabled must not adopt the resident server's reusable
        /// TaskHost. It must instead launch a non-reusable TaskHost for its own build.
        /// </summary>
        [NodeScenarioFact]
        public void TransientBuildDoesNotAdoptResidentSidecars()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            PrepareIsolatedServerEnv(scenario, useServer: false);

            TransientTestFile project = scenario.Environment.CreateFile(
                "residentSidecarVsTransient.proj",
                $"""
                <Project>
                    <UsingTask TaskName="ProcessIdTask" AssemblyFile="{Assembly.GetExecutingAssembly().Location}" />
                    <UsingTask TaskName="SidecarProcessIdTask" AssemblyFile="{Assembly.GetExecutingAssembly().Location}" />
                    <Target Name="Probe">
                        <ProcessIdTask>
                            <Output PropertyName="SERVER_PID" TaskParameter="Pid" />
                        </ProcessIdTask>
                        <SidecarProcessIdTask>
                            <Output PropertyName="SIDECAR_PID" TaskParameter="Pid" />
                        </SidecarProcessIdTask>
                        <Message Text="Server ID is $(SERVER_PID)" Importance="High" />
                        <Message Text="Sidecar ID is $(SIDECAR_PID)" Importance="High" />
                    </Target>
                </Project>
                """);

            ServerBuild resident = Build(scenario, $"{project.Path} -mt -nodeReuse:true", bootstrapped: true);
            resident.ShouldRunOnServer();
            int residentSidecarPid = ParseNumber(resident.Output, "Sidecar ID is ");

            ServerBuild transient = Build(scenario, $"{project.Path} -mt -nodeReuse:false", bootstrapped: true);
            transient.ShouldRunOnServer();
            int transientSidecarPid = ParseNumber(transient.Output, "Sidecar ID is ");

            transient.ServerProcessId.ShouldNotBe(resident.ServerProcessId, "A transient build must run in its own server rather than borrowing the resident one.");
            transientSidecarPid.ShouldNotBe(residentSidecarPid, "A transient build must bring its own TaskHost rather than adopting one owned by the resident server.");
            scenario.Await(NodeScenario.Is(NodeJournalEvent.Launched, NodeJournalKind.TaskHost, processId: transient.ServerProcessId), "the transient server launched its own TaskHost")
                .SubjectProcessId.ShouldBe(transientSidecarPid);
            NodeJournalRecord transientExited = scenario.Await(NodeJournalEvent.Exited, processId: transient.ServerProcessId);
            scenario.AssertNever(NodeScenario.Is(NodeJournalEvent.Exited, processId: resident.ServerProcessId), "the resident server exits", transientExited);

            scenario.ShutdownNodes(ShutdownBootstrapServer);
        }

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
        /// Prepares a server-related test on top of the scenario's isolation (its own handshake salt, so a freshly
        /// launched server): a clean GC environment ensures the server's Server GC comes from the launch injection
        /// rather than an ambient CI/user setting leaking into child nodes.
        /// </summary>
        private static void PrepareIsolatedServerEnv(NodeScenario scenario, bool useServer = true)
        {
            // Child node launches (TaskHost, worker) need DOTNET_HOST_PATH to locate the runtime.
            RunnerUtilities.ApplyDotnetHostPathEnvironmentVariable(scenario.Environment);
            scenario.Environment.SetEnvironmentVariable("DOTNET_gcServer", null);
            scenario.Environment.SetEnvironmentVariable("COMPlus_gcServer", null);
            scenario.Environment.SetEnvironmentVariable("MSBUILDUSESERVER", useServer ? "1" : null);
        }

        /// <summary>
        /// The MSBuild server (build orchestrator) process must be launched with Server GC when the
        /// build is multithreaded (/mt) - that is when the server itself does the project work.
        /// </summary>
        [NodeScenarioFact]
        public void MultiThreadedServerProcessUsesServerGC()
        {
            if (Environment.ProcessorCount < 2)
            {
                Assert.Skip("Server GC can report as Workstation GC on single-processor machines.");
            }

            using NodeScenario scenario = NodeScenario.Create(_output);
            PrepareIsolatedServerEnv(scenario);
            TransientTestFile project = scenario.Environment.CreateFile("serverGcProbe.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));
            ServerBuild build = Build(scenario, $"{project.Path} -mt");

            build.ShouldRunOnServer();
            ParseNumber(build.Output, "TaskRanInPID=").ShouldBe(build.ServerProcessId, "The build should run in the server node, not the entry process.");
            build.Output.ShouldContain("TaskNodeServerGC=True", customMessage: "A multithreaded MSBuild server process should run with Server GC.");

            scenario.ShutdownNodes();
        }

        /// <summary>
        /// Without /mt the server only orchestrates (project work happens in separate worker nodes),
        /// so the server process must keep the default Workstation GC.
        /// </summary>
        [NodeScenarioFact]
        public void NonMultiThreadedServerProcessDoesNotUseServerGC()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            PrepareIsolatedServerEnv(scenario);
            TransientTestFile project = scenario.Environment.CreateFile("serverGcProbeNoMt.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));
            ServerBuild build = Build(scenario, project.Path);

            build.ShouldRunOnServer();
            ParseNumber(build.Output, "TaskRanInPID=").ShouldBe(build.ServerProcessId, "The build should run in the server node, not the entry process.");
            build.Output.ShouldContain("TaskNodeServerGC=False", customMessage: "A non-multithreaded MSBuild server process should keep the default Workstation GC.");

            scenario.ShutdownNodes();
        }

        /// <summary>
        /// A TaskHost process must keep the default Workstation GC, even though a multithreaded server
        /// uses Server GC. Runs two /mt builds against the same server: one in-proc to capture the
        /// Server-GC server PID, then one that forces the task into a TaskHost.
        /// </summary>
        [NodeScenarioFact]
        public void TaskHostProcessDoesNotUseServerGC()
        {
            if (Environment.ProcessorCount < 2)
            {
                Assert.Skip("Server GC can report as Workstation GC on single-processor machines.");
            }

            using NodeScenario scenario = NodeScenario.Create(_output);
            PrepareIsolatedServerEnv(scenario);

            // First /mt build runs the task in-proc in the server node so we can capture the Server-GC server PID.
            TransientTestFile serverProbe = scenario.Environment.CreateFile("serverProbe.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));
            ServerBuild server = Build(scenario, $"{serverProbe.Path} -mt");
            server.ShouldRunOnServer();
            ParseNumber(server.Output, "TaskRanInPID=").ShouldBe(server.ServerProcessId);
            server.Output.ShouldContain("TaskNodeServerGC=True", customMessage: "A multithreaded MSBuild server process should run with Server GC.");

            // Second /mt build (same server) forces the task out-of-proc.
            TransientTestFile taskHostProbe = scenario.Environment.CreateFile("taskHostProbe.proj", GetServerGCProbeProjectContents(useTaskHostFactory: true));
            NodeJournalRecord before = scenario.Marker("TaskHostBuild");
            ServerBuild taskHost = Build(scenario, $"{taskHostProbe.Path} -mt");
            taskHost.ServerProcessId.ShouldBe(server.ServerProcessId);
            int taskHostPid = ParseNumber(taskHost.Output, "TaskRanInPID=");
            scenario.Records.ShouldContain(
                r => r.Sequence > before.Sequence && r.Event == NodeJournalEvent.Launched && r.Kind == NodeJournalKind.TaskHost && r.SubjectProcessId == taskHostPid,
                "The task should run out-of-proc in a TaskHost the server launched.");
            taskHost.Output.ShouldContain("TaskNodeServerGC=False", customMessage: "A TaskHost process must use Workstation GC even when the server uses Server GC.");

            scenario.ShutdownNodes();
        }

        /// <summary>
        /// An out-of-proc worker node must keep the default Workstation GC.
        /// </summary>
        [NodeScenarioFact]
        public void WorkerNodeDoesNotUseServerGC()
        {
            using NodeScenario scenario = NodeScenario.Create(_output);
            PrepareIsolatedServerEnv(scenario, useServer: false);
            scenario.Environment.SetEnvironmentVariable("MSBUILDNOINPROCNODE", "1");
            TransientTestFile project = scenario.Environment.CreateFile("workerGcProbe.proj", GetServerGCProbeProjectContents(useTaskHostFactory: false));
            ServerBuild build = Build(scenario, $"{project.Path} /m:1");

            int workerPid = ParseNumber(build.Output, "TaskRanInPID=");
            scenario.Records.ShouldContain(
                r => r.Event == NodeJournalEvent.Launched && r.Kind == NodeJournalKind.Worker && r.ProcessId == build.ClientProcessId && r.SubjectProcessId == workerPid,
                "The build should run in an out-of-proc worker node, not the entry process.");
            build.Output.ShouldContain("TaskNodeServerGC=False", customMessage: "A worker node must use the default Workstation GC.");

            scenario.ShutdownNodes();
        }
#endif

        /// <summary>
        /// Runs one build and reads from the journal where it ran. When it ran on a server, this also waits until the
        /// server has released its busy mutex (or exited), so the next build of the test finds the server idle rather
        /// than racing its cleanup and falling back in-process (#15093).
        /// </summary>
        private ServerBuild Build(NodeScenario scenario, string arguments, bool bootstrapped = false, bool expectSuccess = true)
        {
            NodeJournalRecord start = scenario.Marker("Build", arguments);
            (bool success, string output) = bootstrapped ? scenario.RunBootstrapped(arguments) : scenario.RunMSBuild(MSBuildExePath, arguments);
            success.ShouldBe(expectSuccess, output);

            NodeJournalRecord[] records = [.. scenario.Records.Where(r => r.Sequence > start.Sequence)];
            NodeJournalRecord client = records.FirstOrDefault(r => r.Role == NodeJournalKind.Main && r.ProcessId != scenario.TestProcessId)
                ?? throw scenario.Fail($"The build '{arguments}' left no record of its entry process.");
            NodeJournalRecord? onServer = records.FirstOrDefault(r => r.Event == NodeJournalEvent.BuildStarted && r.Role == NodeJournalKind.Server);
            string? reuseDecision = records.FirstOrDefault(r => r.Event == NodeJournalEvent.ReuseDecision && r.Kind == NodeJournalKind.Server && r.ProcessId == client.ProcessId)?.Detail;
            bool fellBack = records.Any(r => r.Event == NodeJournalEvent.ServerBusyFallback && r.ProcessId == client.ProcessId);

            if (onServer is not null)
            {
                scenario.Await(
                    r => r.Sequence > onServer.Sequence && r.ProcessId == onServer.ProcessId
                        && (r.Event == NodeJournalEvent.Exited || (r.Event == NodeJournalEvent.BuildEnded && r.Kind == NodeJournalKind.Server)),
                    $"server {onServer.ProcessId} is idle again");
            }

            return new ServerBuild(output, client.ProcessId, onServer?.ProcessId ?? 0, reuseDecision, fellBack);
        }

        private sealed record ServerBuild(string Output, int ClientProcessId, int ServerProcessId, string? ServerReuseDecision, bool FellBackInProc)
        {
            public void ShouldRunOnServer()
            {
                ServerProcessId.ShouldNotBe(0, $"The build should have run on a server node.{Environment.NewLine}{Output}");
                ServerProcessId.ShouldNotBe(ClientProcessId);
            }

            public void ShouldRunInProc(NodeScenario scenario)
            {
                ServerProcessId.ShouldBe(0, "The build should have run in-process, not on a server node.");
                scenario.Records.ShouldContain(
                    r => r.Event == NodeJournalEvent.BuildStarted && r.Kind == NodeJournalKind.InProc && r.ProcessId == ClientProcessId,
                    "The build should have run in its entry process.");
            }
        }

        private static int ParseNumber(string searchString, string toFind)
        {
            Regex regex = new(@$"{toFind}(\d+)");
            Match match = regex.Match(searchString);
            match.Success.ShouldBeTrue($"'{toFind}' was not found in the output:{Environment.NewLine}{searchString}");
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
