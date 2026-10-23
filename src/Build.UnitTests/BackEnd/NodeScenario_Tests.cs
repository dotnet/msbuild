// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.UnitTests.Shared;
using Shouldly;
using Xunit;
using Xunit.Sdk;

#nullable enable

namespace Microsoft.Build.UnitTests.BackEnd;

/// <summary>
/// Tests of the node lifecycle journal across real processes and of the <see cref="NodeScenario"/> harness itself.
/// </summary>
public sealed class NodeScenario_Tests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [NodeScenarioFact]
    public void ConcurrentProcessesShareOneTotalOrder()
    {
        using NodeScenario scenario = NodeScenario.Create(_output);
        TransientTestFile project = scenario.Environment.CreateFile("trivial.proj", """
            <Project>
              <Target Name="Build">
                <Message Text="hello" Importance="high" />
              </Target>
            </Project>
            """);

        NodeScenarioRun[] runs = [.. Enumerable.Range(0, 3).Select(_ => scenario.StartBootstrapped($"\"{project.Path}\" -m:1 -nr:false"))];
        foreach (NodeScenarioRun run in runs)
        {
            run.WaitForSuccess();
        }

        scenario.WaitForQuiescence();
        IReadOnlyList<NodeJournalRecord> records = scenario.Records;

        // One gap-free sequence shared by every writer.
        records.Select(r => r.Sequence).ShouldBe(Enumerable.Range(1, records.Count).Select(i => (long)i));

        // Every build process wrote its own records, in its own program order.
        List<IGrouping<int, NodeJournalRecord>> builds = [.. records
            .Where(r => r.ProcessId != scenario.TestProcessId)
            .GroupBy(r => r.ProcessId)
            .Where(g => g.Any(r => r.Event == NodeJournalEvent.BuildEnded))];
        builds.Count.ShouldBe(runs.Length, string.Join(Environment.NewLine, records));
        foreach (IGrouping<int, NodeJournalRecord> build in builds)
        {
            NodeJournalRecord started = build.First(r => r.Event == NodeJournalEvent.BuildStarted);
            NodeJournalRecord ended = build.First(r => r.Event == NodeJournalEvent.BuildEnded);
            started.Sequence.ShouldBeLessThan(ended.Sequence);
            started.Role.ShouldBe(NodeJournalKind.Main);
        }
    }

    [NodeScenarioFact]
    public void FaultCrashesTheProcessAtThePoint()
    {
        using NodeScenario scenario = NodeScenario.Create(_output);
        TransientTestFile project = scenario.Environment.CreateFile("trivial.proj", """
            <Project>
              <Target Name="Build" />
            </Project>
            """);

        scenario.Fault(NodeJournalEvent.BuildStarted, NodeFaultAction.Crash, NodeJournalKind.Main);
        (bool success, _) = scenario.RunBootstrapped($"\"{project.Path}\" -m:1 -nr:false");
        success.ShouldBeFalse();

        NodeJournalRecord fault = scenario.Await(NodeJournalEvent.FaultInjected, detail: "BuildStarted@Main#1:Crash");
        fault.ProcessId.ShouldNotBe(scenario.TestProcessId);
        scenario.AssertNever(NodeScenario.Is(NodeJournalEvent.BuildEnded, processId: fault.ProcessId), "the crashed process ending its build");
    }

    [NodeScenarioFact]
    public void InProcessBuildManagerIsJournaled()
    {
        using NodeScenario scenario = NodeScenario.Create(_output);
        TransientTestFile project = scenario.Environment.CreateFile("trivial.proj", """
            <Project>
              <Target Name="Build" />
            </Project>
            """);

        BuildManager manager = scenario.CreateBuildManager("InProcScenario");
        BuildResult result = manager.Build(new BuildParameters(), new BuildRequestData(project.Path, new Dictionary<string, string?>(), null, ["Build"], null));
        result.OverallResult.ShouldBe(BuildResultCode.Success);

        scenario.AssertOrder(
            NodeScenario.Is(NodeJournalEvent.BuildStarted, detail: "InProcScenario", processId: scenario.TestProcessId), "in-proc build started",
            NodeScenario.Is(NodeJournalEvent.BuildEnded, detail: "InProcScenario", processId: scenario.TestProcessId), "in-proc build ended");
    }

    [NodeScenarioFact]
    public void AwaitFailsWithTheTimelineWhenNothingCanStillWriteTheRecord()
    {
        using NodeScenario scenario = NodeScenario.Create(_output);
        scenario.Marker("only-record");

        Stopwatch elapsed = Stopwatch.StartNew();
        XunitException failure = Should.Throw<XunitException>(() => scenario.Await(NodeJournalEvent.Launched));

        // Nothing is running, so this must not wait for the hang ceiling.
        elapsed.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(60));
        failure.Message.ShouldContain("Nothing that is still running");
        failure.Message.ShouldContain("Marker");
        failure.Message.ShouldContain("only-record");
    }

    [NodeScenarioFact]
    public void OnlyOneScenarioAtATime()
    {
        using NodeScenario scenario = NodeScenario.Create(_output);
        Should.Throw<InvalidOperationException>(() => NodeScenario.Create(_output));
    }

    [NodeScenarioFact]
    public void ScenarioIsolatesNodeSettings()
    {
        using TestEnvironment outer = TestEnvironment.Create(_output);
        outer.SetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT", "ambient");
        outer.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");
        outer.SetEnvironmentVariable("MSBUILDNODEFAULT", "Connected:Crash");

        string salt;
        using (NodeScenario scenario = NodeScenario.Create(_output))
        {
            salt = scenario.HandshakeSalt;
            Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT").ShouldBe(salt);
            Environment.GetEnvironmentVariable("MSBUILDDISABLENODEREUSE").ShouldBeNull();
            Environment.GetEnvironmentVariable("MSBUILDNODEFAULT").ShouldBeNull();
            Environment.GetEnvironmentVariable("MSBUILDUSESERVER").ShouldBe("0");
            Environment.GetEnvironmentVariable(Traits.NodeJournalEnvVarName).ShouldBe(scenario.JournalPath);
            NodeLifecycleJournal.CurrentSink.ShouldBe(scenario.JournalPath);
        }

        salt.ShouldNotBe("ambient");
        Environment.GetEnvironmentVariable("MSBUILDNODEHANDSHAKESALT").ShouldBe("ambient");
        Environment.GetEnvironmentVariable("MSBUILDDISABLENODEREUSE").ShouldBe("1");
        Environment.GetEnvironmentVariable("MSBUILDNODEFAULT").ShouldBe("Connected:Crash");
        NodeLifecycleJournal.CurrentSink.ShouldBeNull();
    }
}
