// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.Framework.Telemetry;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class CoordinatorTelemetry_Tests(ITestOutputHelper outputHelper)
{
    private readonly TestCoordinatorDebugOutput _output = new(outputHelper);

    [Fact]
    public void RecordGrantIssued_EmitsActivityWithExpectedTags()
    {
        using TestEnvironment env = TestEnvironment.Create(outputHelper);
        env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
        env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", null);

        List<Activity> captured = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.ActivitySourceNamespacePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (captured) { captured.Add(a); } },
        };

        ActivitySource.AddActivityListener(listener);
        TelemetryManager.ResetForTest();
        TelemetryManager.Instance.Initialize(isStandalone: true);

        var connectionId = Guid.NewGuid();
        var grant = new BuildGrant(connectionId, processId: 42, requestedNodes: 8) { GrantedNodes = 4 };
        CoordinatorTelemetry.RecordGrantIssued(
            grant,
            queueDepth: 1,
            activeBuilds: 2,
            allocatedNodes: 12);

        Activity activity = captured.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("VS/MSBuild/Coordinator/Grant");
        activity.GetTagItem("VS.MSBuild.ConnectionId").ShouldBe(connectionId);
        activity.GetTagItem("VS.MSBuild.GrantId").ShouldBe(grant.GrantId);
        activity.GetTagItem("VS.MSBuild.IsNested").ShouldBe(false);
        activity.GetTagItem("VS.MSBuild.ProcessId").ShouldBe(42);
        activity.GetTagItem("VS.MSBuild.NodesRequested").ShouldBe(8);
        activity.GetTagItem("VS.MSBuild.NodesGranted").ShouldBe(4);
        activity.GetTagItem("VS.MSBuild.QueueDepth").ShouldBe(1);
        activity.GetTagItem("VS.MSBuild.ActiveBuilds").ShouldBe(2);
        activity.GetTagItem("VS.MSBuild.AllocatedNodes").ShouldBe(12);
    }

    [Fact]
    public void RecordGrantIssued_NestedGrant_EmitsNestedTag()
    {
        using TestEnvironment env = TestEnvironment.Create(outputHelper);
        env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
        env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", null);

        List<Activity> captured = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.ActivitySourceNamespacePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (captured) { captured.Add(a); } },
        };

        ActivitySource.AddActivityListener(listener);
        TelemetryManager.ResetForTest();
        TelemetryManager.Instance.Initialize(isStandalone: true);

        Guid grantId = Guid.NewGuid();
        var grant = new BuildGrant(Guid.NewGuid(), processId: 42, requestedNodes: 8, grantId, isNested: true)
        {
            GrantedNodes = 4,
        };

        CoordinatorTelemetry.RecordGrantIssued(
            grant,
            queueDepth: 0,
            activeBuilds: 1,
            allocatedNodes: 4);

        Activity activity = captured.ShouldHaveSingleItem();
        activity.GetTagItem("VS.MSBuild.GrantId").ShouldBe(grantId);
        activity.GetTagItem("VS.MSBuild.IsNested").ShouldBe(true);
    }

    [Fact]
    public void RecordGrantDeferred_EmitsActivityWithExpectedTags()
    {
        using TestEnvironment env = TestEnvironment.Create(outputHelper);
        env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
        env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", null);

        List<Activity> captured = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.ActivitySourceNamespacePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (captured) { captured.Add(a); } },
        };

        ActivitySource.AddActivityListener(listener);
        TelemetryManager.ResetForTest();
        TelemetryManager.Instance.Initialize(isStandalone: true);

        var connectionId = Guid.NewGuid();
        var grant = new BuildGrant(connectionId, processId: 99, requestedNodes: 16);
        CoordinatorTelemetry.RecordGrantDeferred(
            grant,
            queueDepth: 3);

        Activity activity = captured.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("VS/MSBuild/Coordinator/Deferred");
        activity.GetTagItem("VS.MSBuild.ConnectionId").ShouldBe(connectionId);
        activity.GetTagItem("VS.MSBuild.GrantId").ShouldBe(grant.GrantId);
        activity.GetTagItem("VS.MSBuild.IsNested").ShouldBe(false);
        activity.GetTagItem("VS.MSBuild.ProcessId").ShouldBe(99);
        activity.GetTagItem("VS.MSBuild.NodesRequested").ShouldBe(16);
        activity.GetTagItem("VS.MSBuild.QueueDepth").ShouldBe(3);
    }

    [Fact]
    public void RecordGrantReleased_EmitsActivityWithExpectedTags()
    {
        using TestEnvironment env = TestEnvironment.Create(outputHelper);
        env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
        env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", null);

        List<Activity> captured = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.ActivitySourceNamespacePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (captured) { captured.Add(a); } },
        };

        ActivitySource.AddActivityListener(listener);
        TelemetryManager.ResetForTest();
        TelemetryManager.Instance.Initialize(isStandalone: true);

        var connectionId = Guid.NewGuid();
        var grant = new BuildGrant(connectionId, processId: 7, requestedNodes: 4) { GrantedNodes = 4 };
        CoordinatorTelemetry.RecordGrantReleased(
            grant,
            queueDepth: 0,
            activeBuilds: 1,
            allocatedNodes: 8);

        Activity activity = captured.ShouldHaveSingleItem();
        activity.DisplayName.ShouldBe("VS/MSBuild/Coordinator/Released");
        activity.GetTagItem("VS.MSBuild.ConnectionId").ShouldBe(connectionId);
        activity.GetTagItem("VS.MSBuild.GrantId").ShouldBe(grant.GrantId);
        activity.GetTagItem("VS.MSBuild.IsNested").ShouldBe(false);
        activity.GetTagItem("VS.MSBuild.ProcessId").ShouldBe(7);
        activity.GetTagItem("VS.MSBuild.NodesReleased").ShouldBe(4);
        activity.GetTagItem("VS.MSBuild.QueueDepth").ShouldBe(0);
        activity.GetTagItem("VS.MSBuild.ActiveBuilds").ShouldBe(1);
        activity.GetTagItem("VS.MSBuild.AllocatedNodes").ShouldBe(8);
    }

    [Fact]
    public async Task NestedGrant_ServerPath_EmitsGrantAndReleaseTelemetry()
    {
        using TestEnvironment env = TestEnvironment.Create(outputHelper);
        env.SetEnvironmentVariable("MSBUILD_TELEMETRY_OPTOUT", null);
        env.SetEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", null);

        List<Activity> captured = [];
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith(TelemetryConstants.ActivitySourceNamespacePrefix, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => { lock (captured) { captured.Add(a); } },
        };

        ActivitySource.AddActivityListener(listener);
        TelemetryManager.ResetForTest();
        TelemetryManager.Instance.Initialize(isStandalone: true);

        string pipeName = NamedPipeUtil.GetPlatformSpecificPipeName($"msbuild-coordinator-test-{Guid.NewGuid():N}");
        using CancellationTokenSource cts = new();
        using CoordinatorServer server = new(
            CoordinatorSettings.Default with
            {
                PipeName = pipeName,
                TotalNodeBudget = 4,
                ShutdownTimeoutMs = Timeout.Infinite,
            },
            _output);

        Task serverTask = server.RunAsync(cts.Token);

        using NamedPipeClientStream rootClient = await ConnectClientPipeAsync(pipeName);
        using BinaryWriter rootWriter = new(rootClient, Encoding.UTF8, leaveOpen: true);
        using BinaryReader rootReader = new(rootClient, Encoding.UTF8, leaveOpen: true);

        SendHandshake(rootWriter, rootReader, processId: 10001);
        rootWriter.Write(new RequestNodesMessage(requestedNodes: 4));

        NodeGrantMessage rootGrant = rootReader.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        using NamedPipeClientStream nestedClient = await ConnectClientPipeAsync(pipeName);
        using BinaryWriter nestedWriter = new(nestedClient, Encoding.UTF8, leaveOpen: true);
        using BinaryReader nestedReader = new(nestedClient, Encoding.UTF8, leaveOpen: true);

        SendHandshake(nestedWriter, nestedReader, processId: 10002);
        nestedWriter.Write(new JoinGrantMessage(rootGrant.GrantId, requestedNodes: 4));
        nestedReader.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        nestedWriter.Write(ReleaseNodesMessage.Instance);
        rootWriter.Write(ReleaseNodesMessage.Instance);
        cts.Cancel();

        await serverTask;

        Activity nestedGrant = captured.Where(activity =>
            activity.DisplayName == "VS/MSBuild/Coordinator/Grant" &&
            activity.GetTagItem("VS.MSBuild.IsNested") is true).ShouldHaveSingleItem();

        nestedGrant.GetTagItem("VS.MSBuild.GrantId").ShouldBe(rootGrant.GrantId);
        nestedGrant.GetTagItem("VS.MSBuild.NodesGranted").ShouldBe(4);

        Activity nestedRelease = captured.Where(activity =>
            activity.DisplayName == "VS/MSBuild/Coordinator/Released" &&
            activity.GetTagItem("VS.MSBuild.IsNested") is true).ShouldHaveSingleItem();

        nestedRelease.GetTagItem("VS.MSBuild.GrantId").ShouldBe(rootGrant.GrantId);
        nestedRelease.GetTagItem("VS.MSBuild.NodesReleased").ShouldBe(4);
    }

    private static async Task<NamedPipeClientStream> ConnectClientPipeAsync(string pipeName)
    {
        NamedPipeClientStream client = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        return client;
    }

    private static void SendHandshake(BinaryWriter writer, BinaryReader reader, int processId)
    {
        writer.Write(new ClientHandshakeMessage(Guid.NewGuid(), processId, [Capabilities.NestedGrants]));
        reader.ReadServerMessage().ShouldBeOfType<ServerHandshakeMessage>();
    }
}

#endif
