// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests;

public sealed class TaskHostBuildCleanup_Tests(ITestOutputHelper output)
{
    internal const string ProbeHostName = "TaskHostCleanupProbe";
    internal const string DisposalGate = "disposal";

    private readonly ITestOutputHelper _output = output;

    /// <summary>
    /// A TaskHost sidecar that is kept for reuse must finish disposing its build-scoped task objects before
    /// <see cref="BuildManager.EndBuild"/> returns, while a sidecar that retires must dispose off the caller's critical
    /// path. Losing the sidecar mid-disposal must release the wait too.
    /// </summary>
    /// <remarks>
    /// The registered task object blocks on a scenario gate while it is disposed, so the test can hold the sidecar in the
    /// middle of cleanup and read from the node lifecycle journal whether EndBuild finished or not.
    /// </remarks>
    [NodeScenarioTheory]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void EndBuildWaitsForDisposalOnlyWhenReusing(bool reuse, bool useWorker, bool crashDuringCleanup)
    {
        using NodeScenario scenario = NodeScenario.Create(_output);
        string assembly = typeof(TaskHostCleanupProbe).Assembly.Location;
        TransientTestFile inner = scenario.Environment.CreateFile("cleanup-inner.proj", $"""
            <Project>
              <UsingTask TaskName="RegisterGatedTaskObject" AssemblyFile="{assembly}" />
              <Target Name="Build" Returns="$(TaskHostPid)">
                <RegisterGatedTaskObject RegisterObject="$(RegisterObject)">
                  <Output TaskParameter="Pid" PropertyName="TaskHostPid" />
                </RegisterGatedTaskObject>
              </Target>
            </Project>
            """);
        TransientTestFile outer = scenario.Environment.CreateFile("cleanup.proj", $"""
            <Project>
              <UsingTask TaskName="TaskHostCleanupProbe" AssemblyFile="{assembly}" />
              <Target Name="Build">
                <TaskHostCleanupProbe Project="{inner.Path}" Reuse="{reuse}" UseWorker="{useWorker}" CrashDuringCleanup="{crashDuringCleanup}" />
              </Target>
            </Project>
            """);

        NodeScenario.GateHandle disposal = scenario.Gate(DisposalGate);
        if (crashDuringCleanup)
        {
            scenario.Fault(NodeJournalEvent.GateEntered, NodeFaultAction.Crash, NodeJournalKind.TaskHost);
        }

        NodeScenarioRun run = scenario.StartBootstrapped($"\"{outer.Path}\" -m:1 -mt:false -nr:false -low:true");
        Func<NodeJournalRecord, bool> probeBuildEnded = NodeScenario.Is(NodeJournalEvent.BuildEnded, detail: ProbeHostName);

        if (crashDuringCleanup)
        {
            // The sidecar dies in the middle of disposing: the connection loss has to end the wait.
            string crashLog = run.WaitForSuccess();
            crashLog.ShouldContain("CleanupPolicyVerified");
            scenario.AssertOrder(
                NodeScenario.Is(NodeJournalEvent.FaultInjected, role: NodeJournalKind.TaskHost), "sidecar crashed during disposal",
                probeBuildEnded, "EndBuild returned");
        }
        else if (reuse)
        {
            NodeJournalRecord entered = disposal.AwaitEntered();
            scenario.AssertNever(probeBuildEnded, "EndBuild returning while the retained sidecar is still disposing", after: entered);
            NodeJournalRecord released = disposal.Release();
            run.WaitForSuccess().ShouldContain("CleanupPolicyVerified");

            NodeJournalRecord disposed = scenario.Await(NodeJournalEvent.DisposalEnd, role: NodeJournalKind.TaskHost, processId: entered.ProcessId);
            NodeJournalRecord ended = scenario.Await(probeBuildEnded, "EndBuild returned");
            released.Sequence.ShouldBeLessThan(disposed.Sequence);
            disposed.Sequence.ShouldBeLessThan(ended.Sequence, "EndBuild must wait for the retained sidecar's disposal");
        }
        else
        {
            NodeJournalRecord entered = disposal.AwaitEntered();

            // The gate is still closed, so this can only succeed if EndBuild does not wait for the disposal.
            scenario.Await(probeBuildEnded, "EndBuild returned while the retiring sidecar is still disposing");
            disposal.Release();
            scenario.Await(NodeJournalEvent.DisposalEnd, role: NodeJournalKind.TaskHost, processId: entered.ProcessId);
            run.WaitForSuccess().ShouldContain("CleanupPolicyVerified");
        }

        // One sidecar (reused by the second build when reusing), one worker, and all of them shut down.
        scenario.Count(NodeScenario.Is(NodeJournalEvent.Launched, NodeJournalKind.TaskHost)).ShouldBe(1);
        NodeJournalRecord sidecar = scenario.Await(NodeJournalEvent.Launched, NodeJournalKind.TaskHost);
        if (!crashDuringCleanup)
        {
            scenario.Await(NodeJournalEvent.Exited, processId: sidecar.SubjectProcessId);
        }

        if (useWorker)
        {
            scenario.Count(NodeScenario.Is(NodeJournalEvent.Launched, NodeJournalKind.Worker)).ShouldBe(1);
            NodeJournalRecord worker = scenario.Await(NodeJournalEvent.Launched, NodeJournalKind.Worker);
            scenario.Await(NodeJournalEvent.Exited, processId: worker.SubjectProcessId);
        }
    }
}

/// <summary>
/// Runs inside the bootstrapped MSBuild: builds the inner project with a private <see cref="BuildManager"/> whose tasks
/// run in a TaskHost sidecar, once or (when reusing) twice, and checks what can only be checked in-process.
/// Everything that involves timing is asserted by the test from the node lifecycle journal.
/// </summary>
public sealed class TaskHostCleanupProbe : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Project { get; set; } = null!;

    public bool Reuse { get; set; }

    public bool UseWorker { get; set; }

    public bool CrashDuringCleanup { get; set; }

    public override bool Execute()
    {
        // TestEnvironment would reset the default BuildManager that is executing this task.
        string? originalForceOutOfProc = Environment.GetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC");
        if (UseWorker)
        {
            Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
        }

        try
        {
            using BuildManager manager = new(TaskHostBuildCleanup_Tests.ProbeHostName);
            if (UseWorker)
            {
                ((IBuildComponentHost)manager).RegisterFactory(
                    BuildComponentType.OutOfProcNodeProvider, _ => new RetainingWorkerProvider());
            }

            MockLogger logger = new();
            BuildParameters parameters = new()
            {
                MultiThreaded = !UseWorker,
                DisableInProcNode = UseWorker,
                MaxNodeCount = 1,
                EnableNodeReuse = Reuse,
                LowPriority = true,
                Loggers = [logger]
            };

            manager.BeginBuild(parameters);
            BuildResult result = manager.PendBuildRequest(CreateRequest(registerObject: true)).Execute();
            result.OverallResult.ShouldBe(BuildResultCode.Success, logger.FullLog);
            string sidecarPid = result.ResultsByTarget["Build"].Items[0].ItemSpec;
            int? workerPid = null;
            if (UseWorker)
            {
                using Process worker = Process.GetProcessById(manager.GetWorkerProcesses().ShouldHaveSingleItem().Id);
                workerPid = worker.Id;
                Log.LogMessage(MessageImportance.High, "WorkerPid={0}; WorkerPriority={1}; RequestedLowPriority={2}",
                    worker.Id, worker.PriorityClass, parameters.LowPriority);
                worker.PriorityClass.ShouldBe(ProcessPriorityClass.BelowNormal, "the worker's actual priority must match its reuse handshake");
            }

            // Blocks on the disposal gate for as long as the test holds it, when the sidecar is retained.
            manager.EndBuild();

            if (Reuse && !CrashDuringCleanup)
            {
                manager.BeginBuild(parameters);
                BuildResult next = manager.PendBuildRequest(CreateRequest(registerObject: false)).Execute();
                next.OverallResult.ShouldBe(BuildResultCode.Success, logger.FullLog);
                next.ResultsByTarget["Build"].Items[0].ItemSpec.ShouldBe(sidecarPid, "the retained sidecar must be reused");
                if (workerPid is not null)
                {
                    manager.GetWorkerProcesses().ShouldHaveSingleItem().Id.ShouldBe(workerPid.Value, "the recreated node must use the same worker process");
                }

                manager.EndBuild();
            }

            // ShutdownAllNodes gives every candidate one short connection attempt per handshake variant, so a reused node
            // that is still re-listening after rejecting the other variant is missed. Retry until the reused node is gone.
            int? reusedPid = !Reuse ? null : workerPid ?? (CrashDuringCleanup ? null : int.Parse(sidecarPid, CultureInfo.InvariantCulture));
            manager.ShutdownAllNodes();
            while (reusedPid is int pid && IsRunning(pid))
            {
                Thread.Sleep(100);
                manager.ShutdownAllNodes();
            }

            Log.LogMessage(MessageImportance.High, "CleanupPolicyVerified Reuse={0} Worker={1} WorkerPid={2} SidecarPid={3}",
                Reuse, UseWorker, workerPid, sidecarPid);
            return true;
        }
        finally
        {
            Environment.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", originalForceOutOfProc);
        }
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private BuildRequestData CreateRequest(bool registerObject) => new(
        Project,
        new Dictionary<string, string?>
        {
            ["RegisterObject"] = registerObject.ToString()
        },
        null,
        ["Build"],
        null);

    private sealed class RetainingWorkerProvider : NodeProviderOutOfProc
    {
        protected override int GetNodeReuseThreshold() => int.MaxValue;
    }
}

public sealed class RegisterGatedTaskObject : Microsoft.Build.Utilities.Task
{
    public bool RegisterObject { get; set; }

    [Output]
    public int Pid { get; set; }

    public override bool Execute()
    {
        Pid = EnvironmentUtilities.CurrentProcessId;
        if (RegisterObject)
        {
#pragma warning disable CA2000 // MSBuild disposes the registered object.
            ((IBuildEngine4)BuildEngine).RegisterTaskObject(
                TaskHostBuildCleanup_Tests.DisposalGate, new GatedDisposal(), RegisteredTaskObjectLifetime.Build, allowEarlyCollection: false);
#pragma warning restore CA2000
        }

        return true;
    }

    private sealed class GatedDisposal : IDisposable
    {
        public void Dispose() => NodeScenarioGate.Enter(TaskHostBuildCleanup_Tests.DisposalGate);
    }
}
