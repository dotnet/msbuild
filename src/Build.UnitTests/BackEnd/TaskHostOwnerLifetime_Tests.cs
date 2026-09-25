// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;

#nullable enable

namespace Microsoft.Build.UnitTests;

public sealed class TaskHostOwnerLifetime_Tests(ITestOutputHelper output)
{
    internal const int Rounds = 3;

    internal static string ReleasedGate(int round) => $"released{round}";

    internal static string HoldGate(int round) => $"hold{round}";

    internal static string ActiveGate(int round) => $"active{round}";

    internal static string ShutdownGate(int round) => $"shutdown{round}";

    [NodeScenarioFact]
    public void LateCallbackReplyCannotReachReplacementTaskHost()
    {
        using NodeScenario scenario = NodeScenario.Create(output);
        TestEnvironment env = scenario.Environment;
        env.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0");
        env.SetEnvironmentVariable("MSBUILDFORCEALLTASKSOUTOFPROC", "1");
        scenario.UseShortNodeIdleTimeout();
        string assembly = typeof(SidecarProcessProbe).Assembly.Location;
        TransientTestFile outerPid = env.CreateFile("outer.pid", "");
        TransientTestFile replacementPid = env.CreateFile("replacement.pid", "");
        TransientTestFile finalPid = env.CreateFile("final.pid", "");
        TransientTestFile project = env.CreateFile("nestedCrash.proj", $"""
            <Project>
              <UsingTask TaskName="NestedCrashCallTask" AssemblyFile="{assembly}" />
              <UsingTask TaskName="ExitTaskHostTask" AssemblyFile="{assembly}" />
              <UsingTask TaskName="SidecarProcessProbe" AssemblyFile="{assembly}" />
              <Target Name="Build">
                <NestedCrashCallTask ReportFile="{outerPid.Path}" ContinueOnError="ErrorAndContinue" />
                <SidecarProcessProbe ReportFile="{finalPid.Path}" />
              </Target>
              <Target Name="CrashAndContinue">
                <ExitTaskHostTask ContinueOnError="ErrorAndContinue" />
                <SidecarProcessProbe ReportFile="{replacementPid.Path}" />
              </Target>
            </Project>
            """);

        (bool success, string log) = scenario.RunBootstrapped($"\"{project.Path}\" -m:1 -mt:false -nr:true");
        success.ShouldBeFalse("the deliberate TaskHost crash fails the build");
        int original = ReadPid(outerPid, log);
        int replacement = ReadPid(replacementPid, log);
        int last = ReadPid(finalPid, log);
        output.WriteLine($"OriginalTaskHost={original}; ReplacementTaskHost={replacement}; FinalTaskHost={last}");

        replacement.ShouldNotBe(original);
        last.ShouldBe(replacement, "the old callback reply must not kill the replacement TaskHost");
        scenario.AssertOrder(
            r => IsTaskHostLaunch(r, original), "original TaskHost launched",
            r => IsTaskHostLaunch(r, replacement), "replacement TaskHost launched");
        scenario.Count(r => r.Event == NodeJournalEvent.Launched && r.Kind == NodeJournalKind.TaskHost)
            .ShouldBe(2, "only the crashed TaskHost may be replaced; a replacement that is torn down is replaced again");

        // Nothing that the bootstrap can run reaches its pooled TaskHost, so let it end on its idle timeout.
        scenario.ShutdownNodes(static () => { });
        scenario.Await(NodeJournalEvent.Exited, processId: replacement);
    }

    /// <summary>
    /// A sidecar TaskHost belongs to the <see cref="BuildManager"/> that created it: it must exit as soon as its owner
    /// releases it (disposal, a build without node reuse, ShutdownAllNodes, or after an active build), while the process
    /// hosting that owner keeps running. Idle-node shutdown must not interrupt a sidecar that is running a task.
    /// </summary>
    /// <remarks>
    /// The probe runs three owners in turn in the bootstrapped MSBuild and blocks on a gate once each has released its
    /// sidecar, so the test can check from the journal that the sidecar exits while its host process is still alive.
    /// </remarks>
    [NodeScenarioTheory]
    [InlineData("dispose")]
    [InlineData("reuse-off")]
    [InlineData("shutdown-all")]
    [InlineData("active-shutdown")]
    [InlineData("active-child-crash")]
    public void HostedSidecarsExitWhenTheirOwnerReleasesThem(string release)
    {
        using NodeScenario scenario = NodeScenario.Create(output);
        scenario.Environment.SetEnvironmentVariable("MSBUILDFORCEMULTITHREADED", "0");
        string assembly = typeof(HostingLifetimeProbe).Assembly.Location;
        TransientTestFile inner = scenario.Environment.CreateFile("inner.proj", $"""
            <Project>
              <UsingTask TaskName="SidecarProcessProbe" AssemblyFile="{assembly}" />
              <Target Name="Build" Returns="$(HostPid)">
                <SidecarProcessProbe>
                  <Output TaskParameter="Pid" PropertyName="HostPid" />
                </SidecarProcessProbe>
              </Target>
              <Target Name="Empty" />
              <UsingTask TaskName="SidecarHoldTask" AssemblyFile="{assembly}" />
              <Target Name="Hold">
                <SidecarHoldTask Gate="$(HoldGate)" />
              </Target>
            </Project>
            """);
        TransientTestFile outer = scenario.Environment.CreateFile("outer.proj", $"""
            <Project>
              <UsingTask TaskName="{nameof(HostingLifetimeProbe)}" AssemblyFile="{assembly}" />
              <Target Name="Build">
                <HostingLifetimeProbe Project="{inner.Path}" Release="{release}" />
              </Target>
            </Project>
            """);

        bool crash = release == "active-child-crash";
        if (crash)
        {
            // Every sidecar dies as soon as its task holds.
            scenario.Fault(NodeJournalEvent.GateEntered, NodeFaultAction.Crash, NodeJournalKind.TaskHost);
        }

        NodeScenarioRun run = scenario.StartBootstrapped($"\"{outer.Path}\" -m:1 -mt:false -nr:false");
        NodeJournalRecord? previous = null;
        for (int round = 0; round < Rounds; round++)
        {
            NodeScenario.GateHandle released = scenario.Gate(ReleasedGate(round));
            long after = previous?.Sequence ?? 0;
            NodeJournalRecord launched = scenario.Await(
                r => r.Sequence > after && r.Event == NodeJournalEvent.Launched && r.Kind == NodeJournalKind.TaskHost,
                $"sidecar of owner {round} launched");
            int sidecar = launched.SubjectProcessId;

            if (release == "active-shutdown")
            {
                NodeScenario.GateHandle hold = scenario.Gate(HoldGate(round));
                NodeScenario.GateHandle active = scenario.Gate(ActiveGate(round));
                NodeScenario.GateHandle shutdown = scenario.Gate(ShutdownGate(round));
                NodeJournalRecord holding = hold.AwaitEntered();
                holding.ProcessId.ShouldBe(sidecar);
                active.AwaitEntered();
                active.Release();
                shutdown.AwaitEntered();
                scenario.AssertNever(
                    r => r.ProcessId == sidecar && r.Event is NodeJournalEvent.Exited or NodeJournalEvent.Disconnected,
                    "idle-node shutdown interrupting the sidecar that runs a task",
                    after: holding);
                hold.Release();
                shutdown.Release();
            }
            else if (crash)
            {
                scenario.Await(NodeJournalEvent.FaultInjected, role: NodeJournalKind.TaskHost, processId: sidecar);
            }

            NodeJournalRecord ownerDone = released.AwaitEntered();
            if (!crash)
            {
                // The owner's process is blocked on the gate, so only releasing the sidecar can have ended it.
                scenario.Await(NodeJournalEvent.Exited, processId: sidecar);
            }

            previous = released.Release();
            ownerDone.ProcessId.ShouldNotBe(sidecar);
        }

        run.WaitForSuccess().ShouldContain($"OwnersCompleted={Rounds}");
        scenario.Count(r => r.Event == NodeJournalEvent.Launched && r.Kind == NodeJournalKind.TaskHost).ShouldBe(Rounds);
    }

    private static bool IsTaskHostLaunch(NodeJournalRecord record, int processId)
        => record.Event == NodeJournalEvent.Launched && record.Kind == NodeJournalKind.TaskHost && record.SubjectProcessId == processId;

    private static int ReadPid(TransientTestFile file, string log)
    {
        string text = File.ReadAllText(file.Path);
        text.ShouldNotBeEmpty(log);
        return int.Parse(text, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Runs inside the bootstrapped MSBuild: creates <see cref="TaskHostOwnerLifetime_Tests.Rounds"/> multi-threaded owners
/// in turn, each with a reusable sidecar, releases each sidecar the way <see cref="Release"/> says, and then blocks on a
/// gate so that the test can observe the sidecar's exit while this process stays alive.
/// </summary>
public sealed class HostingLifetimeProbe : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Project { get; set; } = null!;

    [Required]
    public string Release { get; set; } = null!;

    public override bool Execute()
    {
        for (int round = 0; round < TaskHostOwnerLifetime_Tests.Rounds; round++)
        {
            if (Release is "active-shutdown" or "active-child-crash")
            {
                RunActiveScenario(round);
            }
            else if (!RunIdleScenario(round))
            {
                return false;
            }

            if (Release is "dispose" or "active-shutdown" or "active-child-crash")
            {
                NodeScenarioGate.Enter(TaskHostOwnerLifetime_Tests.ReleasedGate(round));
            }
        }

        Log.LogMessage(MessageImportance.High, "OwnersCompleted={0}", TaskHostOwnerLifetime_Tests.Rounds);
        return true;
    }

    private bool RunIdleScenario(int round)
    {
        using BuildManager manager = new();
        MockLogger logger = new();
        BuildParameters parameters = new()
        {
            MultiThreaded = true,
            MaxNodeCount = 1,
            EnableNodeReuse = true,
            Loggers = [logger]
        };
        BuildResult first = manager.Build(parameters, CreateRequest("Build"));
        if (first.OverallResult != BuildResultCode.Success)
        {
            Log.LogError(logger.FullLog);
            return false;
        }

        string childPid = first.ResultsByTarget["Build"].Items[0].ItemSpec;
        Log.LogMessage(MessageImportance.High, "HostedSidecar Owner={0} Child={1} Release={2} Round={3}", EnvironmentUtilities.CurrentProcessId, childPid, Release, round);
        if (childPid == EnvironmentUtilities.CurrentProcessId.ToString(CultureInfo.InvariantCulture))
        {
            Log.LogError("The probe did not create a sidecar.");
            return false;
        }

        BuildResult second = manager.Build(parameters, CreateRequest("Build"));
        if (second.OverallResult != BuildResultCode.Success || second.ResultsByTarget["Build"].Items[0].ItemSpec != childPid)
        {
            Log.LogError("The owner did not reuse its sidecar.");
            return false;
        }

        switch (Release)
        {
            case "dispose":
                return true;
            case "reuse-off":
                parameters.EnableNodeReuse = false;
                manager.Build(parameters, CreateRequest("Empty")).OverallResult.ShouldBe(BuildResultCode.Success);
                break;
            case "shutdown-all":
                manager.ShutdownAllNodes();
                break;
            default:
                throw new ArgumentException("Unknown lifetime scenario.", nameof(Release));
        }

        // The owner is still alive and undisposed here.
        NodeScenarioGate.Enter(TaskHostOwnerLifetime_Tests.ReleasedGate(round));
        return true;
    }

    private void RunActiveScenario(int round)
    {
        using BuildManager manager = new();
        MockLogger logger = new();
        BuildParameters parameters = new() { MultiThreaded = true, MaxNodeCount = 1, EnableNodeReuse = true, Loggers = [logger] };
        manager.BeginBuild(parameters);
        try
        {
            BuildSubmission submission = manager.PendBuildRequest(new BuildRequestData(
                Project, new Dictionary<string, string?> { ["HoldGate"] = TaskHostOwnerLifetime_Tests.HoldGate(round) }, null, ["Hold"], null));
            submission.ExecuteAsync(null, null);

            if (Release == "active-shutdown")
            {
                // The test lets this through once the sidecar holds its task.
                NodeScenarioGate.Enter(TaskHostOwnerLifetime_Tests.ActiveGate(round));
                manager.ShutdownAllNodes();
                submission.WaitHandle.WaitOne(0).ShouldBeFalse("idle-node shutdown must not interrupt an active task");
                NodeScenarioGate.Enter(TaskHostOwnerLifetime_Tests.ShutdownGate(round));
            }

            submission.WaitHandle.WaitOne();
            submission.BuildResult.ShouldNotBeNull().OverallResult.ShouldBe(
                Release == "active-shutdown" ? BuildResultCode.Success : BuildResultCode.Failure, logger.FullLog);
        }
        finally
        {
            manager.EndBuild();
        }

        var provider = (NodeProviderOutOfProcTaskHost)((IBuildComponentHost)manager).GetComponent(BuildComponentType.OutOfProcTaskHostNodeProvider);
        provider.TaskHandlerRegistrationCount.ShouldBe(0);
        Log.LogMessage(MessageImportance.High, "ActiveCaseCompleted=True; HandlerRegistrations={0}", provider.TaskHandlerRegistrationCount);
    }

    private BuildRequestData CreateRequest(string target) => new(Project, new Dictionary<string, string?>(), null, [target], null);
}

public sealed class SidecarProcessProbe : Microsoft.Build.Utilities.Task
{
    [Output]
    public int Pid { get; set; }

    public string? ReportFile { get; set; }

    public override bool Execute()
    {
        using Process process = Process.GetCurrentProcess();
        Pid = process.Id;
        if (ReportFile is not null)
        {
            File.WriteAllText(ReportFile, Pid.ToString(CultureInfo.InvariantCulture));
        }
        return true;
    }
}

public sealed class NestedCrashCallTask : Microsoft.Build.Utilities.Task
{
    public string ReportFile { get; set; } = null!;

    public override bool Execute()
    {
        using Process process = Process.GetCurrentProcess();
        File.WriteAllText(ReportFile, process.Id.ToString(CultureInfo.InvariantCulture));
        return BuildEngine.BuildProjectFile(BuildEngine.ProjectFileOfTaskNode, ["CrashAndContinue"], null, null);
    }
}

public sealed class ExitTaskHostTask : Microsoft.Build.Utilities.Task
{
    public override bool Execute()
    {
        Environment.Exit(23);
        return false;
    }
}

/// <summary>Holds the TaskHost that runs it busy on a scenario gate until the test releases it.</summary>
public sealed class SidecarHoldTask : Microsoft.Build.Utilities.Task
{
    [Required]
    public string Gate { get; set; } = null!;

    public override bool Execute()
    {
        NodeScenarioGate.Enter(Gate);
        return true;
    }
}
