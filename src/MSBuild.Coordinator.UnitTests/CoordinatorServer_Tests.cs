// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Text;
using System.Collections.Immutable;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class CoordinatorServer_Tests(ITestOutputHelper testOutput) : IDisposable
{
    private readonly string _pipeName = NamedPipeUtil.GetPlatformSpecificPipeName($"msbuild-coordinator-test-{Guid.NewGuid():N}");
    private readonly CancellationTokenSource _cts = new();

    private readonly TestCoordinatorDebugOutput _output = new(testOutput);

    private CoordinatorSettings DefaultSettings => CoordinatorSettings.Default with
    {
        PipeName = _pipeName,
        ShutdownTimeoutMs = Timeout.Infinite,
    };

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    [Fact]
    public async Task SingleClient_ReceivesNodeGrant()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientPipeAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        ServerHandshakeMessage handshake = SendHandshake(writer, reader);
        handshake.Capabilities.ShouldContain(Capabilities.Priority);
        writer.Write(new RequestNodesMessage(requestedNodes: 16));

        ServerMessage response = reader.ReadServerMessage();

        response.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(16);

        writer.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task RunAsync_DebugOutputUsesEffectiveNodePolicyValues()
    {
        CoordinatorSettings settings = DefaultSettings with
        {
            TotalNodeBudget = 8,
            HighPriorityReservedNodes = 100,
            MaxNodesPerBuild = 0,
            PriorityAgingThreshold = 5,
        };

        using CoordinatorServer server = CreateServer(settings);
        Task serverTask = server.RunAsync(_cts.Token);

        _output.Lines.ShouldContain(
            line => line.Contains("high-priority reserved nodes=7")
                && line.Contains("max nodes per build=0 (uncapped)")
                && line.Contains("priority aging threshold=5"));

        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task SingleClient_PriorityRequest_ReceivesNodeGrant()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientPipeAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer, reader);
        writer.Write(new RequestNodesWithPriorityMessage(requestedNodes: 4, CoordinatorBuildPriority.High));

        ServerMessage response = reader.ReadServerMessage();

        response.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        writer.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task SingleClient_RequestLessThanBudget_GrantsOnlyRequested()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientPipeAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer, reader);
        writer.Write(new RequestNodesMessage(requestedNodes: 4));

        ServerMessage response = reader.ReadServerMessage();

        response.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        writer.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task ClientWithNestedGrantCapability_ReceivesGrantId()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientPipeAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer, reader, supportsNestedGrants: true);
        writer.Write(new RequestNodesMessage(requestedNodes: 4));

        NodeGrantMessage response = reader.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();
        response.GrantedNodes.ShouldBe(4);
        response.GrantId.ShouldNotBe(Guid.Empty);

        writer.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task ClientWithoutNestedGrantCapability_ReceivesLegacyNodeGrant()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientPipeAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer, reader, supportsNestedGrants: false);
        writer.Write(new RequestNodesMessage(requestedNodes: 4));

        reader.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        writer.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task JoinGrantWithoutNestedGrantCapability_ReceivesError()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientPipeAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer, reader, supportsNestedGrants: false);
        writer.Write(new JoinGrantMessage(Guid.NewGuid(), requestedNodes: 4));

        reader.ReadServerMessage().ShouldBeOfType<ErrorMessage>();

        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task NestedClient_JoinsRootGrant_WithoutConsumingAdditionalBudget()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream rootClient = await ConnectClientPipeAsync();
        using BinaryWriter rootWriter = new(rootClient, Encoding.UTF8, leaveOpen: true);
        using BinaryReader rootReader = new(rootClient, Encoding.UTF8, leaveOpen: true);

        SendHandshake(rootWriter, rootReader, processId: 10001, supportsNestedGrants: true);
        rootWriter.Write(new RequestNodesMessage(requestedNodes: 4));

        NodeGrantMessage rootGrant = rootReader.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();
        rootGrant.GrantedNodes.ShouldBe(4);

        using NamedPipeClientStream nestedClient = await ConnectClientPipeAsync();
        using BinaryWriter nestedWriter = new(nestedClient, Encoding.UTF8, leaveOpen: true);
        using BinaryReader nestedReader = new(nestedClient, Encoding.UTF8, leaveOpen: true);

        SendHandshake(nestedWriter, nestedReader, processId: 10002, supportsNestedGrants: true);
        nestedWriter.Write(new JoinGrantMessage(rootGrant.GrantId, requestedNodes: 4));

        NodeGrantMessage nestedGrant = nestedReader.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();
        nestedGrant.GrantedNodes.ShouldBe(4);
        nestedGrant.GrantId.ShouldBe(rootGrant.GrantId);

        using NamedPipeClientStream unrelatedClient = await ConnectClientPipeAsync();
        using BinaryWriter unrelatedWriter = new(unrelatedClient, Encoding.UTF8, leaveOpen: true);
        using BinaryReader unrelatedReader = new(unrelatedClient, Encoding.UTF8, leaveOpen: true);

        SendHandshake(unrelatedWriter, unrelatedReader, processId: 10003, supportsNestedGrants: true);
        unrelatedWriter.Write(new RequestNodesMessage(requestedNodes: 4));
        unrelatedReader.ReadServerMessage().ShouldBeOfType<WaitMessage>();

        nestedWriter.Write(ReleaseNodesMessage.Instance);
        Task<ServerMessage> unrelatedGrantTask = Task.Run(() => unrelatedReader.ReadServerMessage());
        Task completedBeforeRootRelease = await Task.WhenAny(unrelatedGrantTask, Task.Delay(200));
        completedBeforeRootRelease.ShouldNotBeSameAs(unrelatedGrantTask);

        rootWriter.Write(ReleaseNodesMessage.Instance);
        (await unrelatedGrantTask).ShouldBeOfType<NodeGrantMessage>();

        unrelatedWriter.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task MultipleClients_FairShareAllocation()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 8);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client takes all 8 nodes.
        using NamedPipeClientStream client1 = await ConnectClientPipeAsync();
        using BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer1, reader1, processId: 10001);
        writer1.Write(new RequestNodesMessage(requestedNodes: 8));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(8);

        // Second client should get a Wait.
        using NamedPipeClientStream client2 = await ConnectClientPipeAsync();
        using BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer2, reader2, processId: 10002);
        writer2.Write(new RequestNodesMessage(requestedNodes: 8));
        reader2.ReadServerMessage().ShouldBeOfType<WaitMessage>();

        // First client releases. Second client should receive a NodeGrant.
        writer1.Write(ReleaseNodesMessage.Instance);

        NodeGrantMessage grantAfterWait = reader2.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();
        grantAfterWait.GrantedNodes.ShouldBeGreaterThan(0);

        writer2.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task MultipleWaiters_FairShareDistribution()
    {
        // Budget of 8. One active client holds all 8. Two clients wait.
        // When the active client releases, both waiters should get fair-share grants.
        using CoordinatorServer server = CreateServer(totalNodeBudget: 8);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client takes all 8 nodes.
        using NamedPipeClientStream client1 = await ConnectClientPipeAsync();
        using BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer1, reader1, processId: 10001);
        writer1.Write(new RequestNodesMessage(requestedNodes: 8));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(8);

        // Second client waits.
        using NamedPipeClientStream client2 = await ConnectClientPipeAsync();
        using BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer2, reader2, processId: 10002);
        writer2.Write(new RequestNodesMessage(requestedNodes: 8));
        reader2.ReadServerMessage().ShouldBeOfType<WaitMessage>();

        // Third client waits.
        using NamedPipeClientStream client3 = await ConnectClientPipeAsync();
        using BinaryWriter writer3 = new(client3, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader3 = new(client3, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer3, reader3, processId: 10003);
        writer3.Write(new RequestNodesMessage(requestedNodes: 8));
        reader3.ReadServerMessage().ShouldBeOfType<WaitMessage>();

        // Release first client. Both waiters should get grants.
        writer1.Write(ReleaseNodesMessage.Instance);

        NodeGrantMessage grant2 = reader2.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();
        NodeGrantMessage grant3 = reader3.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        // Both should get nodes, and together they shouldn't exceed budget.
        grant2.GrantedNodes.ShouldBeGreaterThan(0);
        grant3.GrantedNodes.ShouldBeGreaterThan(0);
        (grant2.GrantedNodes + grant3.GrantedNodes).ShouldBeLessThanOrEqualTo(8);

        writer2.Write(ReleaseNodesMessage.Instance);
        writer3.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task WaitingClientDisconnects_DoesNotAffectOtherWaiters()
    {
        // Budget of 4. One active client holds all 4. Two clients wait.
        // The first waiter disconnects. When the active client releases,
        // the remaining waiter should get a grant.
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        // Active client takes all 4 nodes.
        using NamedPipeClientStream client1 = await ConnectClientPipeAsync();
        using BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer1, reader1, processId: 10001);
        writer1.Write(new RequestNodesMessage(requestedNodes: 4));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        // First waiter.
        NamedPipeClientStream client2 = await ConnectClientPipeAsync();
        BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer2, reader2, processId: 10002);
        writer2.Write(new RequestNodesMessage(requestedNodes: 4));
        reader2.ReadServerMessage().ShouldBeOfType<WaitMessage>();

        // Second waiter.
        using NamedPipeClientStream client3 = await ConnectClientPipeAsync();
        using BinaryWriter writer3 = new(client3, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader3 = new(client3, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer3, reader3, processId: 10003);
        writer3.Write(new RequestNodesMessage(requestedNodes: 4));
        reader3.ReadServerMessage().ShouldBeOfType<WaitMessage>();

        // First waiter disconnects abruptly.
        writer2.Dispose();
        reader2.Dispose();
        client2.Dispose();

        // Active client releases. The server will attempt to grant the disconnected
        // waiter, fail with IOException, and then grant the remaining waiter.
        writer1.Write(ReleaseNodesMessage.Instance);

        ServerMessage grant = reader3.ReadServerMessage();
        grant.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBeGreaterThan(0);

        writer3.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task HighPriorityReserve_LeavesCapacityForHighPriorityClient()
    {
        using CoordinatorServer server = CreateServer(DefaultSettings with
        {
            TotalNodeBudget = 4,
            HighPriorityReservedNodes = 1,
            MaxNodesPerBuild = 0,
        });
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream normalClient = await ConnectClientPipeAsync();
        using BinaryWriter normalWriter = new(normalClient, Encoding.UTF8, leaveOpen: true);
        using BinaryReader normalReader = new(normalClient, Encoding.UTF8, leaveOpen: true);

        SendHandshake(normalWriter, normalReader, processId: 10001, capabilities: [Capabilities.Priority]);
        normalWriter.Write(new RequestNodesWithPriorityMessage(requestedNodes: 4, CoordinatorBuildPriority.Normal));
        normalReader.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(3);

        using NamedPipeClientStream highClient = await ConnectClientPipeAsync();
        using BinaryWriter highWriter = new(highClient, Encoding.UTF8, leaveOpen: true);
        using BinaryReader highReader = new(highClient, Encoding.UTF8, leaveOpen: true);

        SendHandshake(highWriter, highReader, processId: 10002, capabilities: [Capabilities.Priority]);
        highWriter.Write(new RequestNodesWithPriorityMessage(requestedNodes: 4, CoordinatorBuildPriority.High));

        highReader.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(1);

        highWriter.Write(ReleaseNodesMessage.Instance);
        normalWriter.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task AutoStrictPolicy_CapsNormalAndReservesHighPriorityCapacity()
    {
        using CoordinatorServer server = CreateServer(DefaultSettings with
        {
            TotalNodeBudget = 16,
            HighPriorityReservedNodes = CoordinatorSettings.AutoNodeConfiguration,
            MaxNodesPerBuild = CoordinatorSettings.AutoNodeConfiguration,
        });
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream normalClient1 = await ConnectClientPipeAsync();
        using BinaryWriter normalWriter1 = new(normalClient1, Encoding.UTF8, leaveOpen: true);
        using BinaryReader normalReader1 = new(normalClient1, Encoding.UTF8, leaveOpen: true);

        SendHandshake(normalWriter1, normalReader1, processId: 10001, capabilities: [Capabilities.Priority]);
        normalWriter1.Write(new RequestNodesWithPriorityMessage(requestedNodes: 16, CoordinatorBuildPriority.Normal));
        normalReader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        using NamedPipeClientStream normalClient2 = await ConnectClientPipeAsync();
        using BinaryWriter normalWriter2 = new(normalClient2, Encoding.UTF8, leaveOpen: true);
        using BinaryReader normalReader2 = new(normalClient2, Encoding.UTF8, leaveOpen: true);

        SendHandshake(normalWriter2, normalReader2, processId: 10002, capabilities: [Capabilities.Priority]);
        normalWriter2.Write(new RequestNodesWithPriorityMessage(requestedNodes: 16, CoordinatorBuildPriority.Normal));
        normalReader2.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        using NamedPipeClientStream normalClient3 = await ConnectClientPipeAsync();
        using BinaryWriter normalWriter3 = new(normalClient3, Encoding.UTF8, leaveOpen: true);
        using BinaryReader normalReader3 = new(normalClient3, Encoding.UTF8, leaveOpen: true);

        SendHandshake(normalWriter3, normalReader3, processId: 10003, capabilities: [Capabilities.Priority]);
        normalWriter3.Write(new RequestNodesWithPriorityMessage(requestedNodes: 16, CoordinatorBuildPriority.Normal));
        normalReader3.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        using NamedPipeClientStream normalClient4 = await ConnectClientPipeAsync();
        using BinaryWriter normalWriter4 = new(normalClient4, Encoding.UTF8, leaveOpen: true);
        using BinaryReader normalReader4 = new(normalClient4, Encoding.UTF8, leaveOpen: true);

        SendHandshake(normalWriter4, normalReader4, processId: 10004, capabilities: [Capabilities.Priority]);
        normalWriter4.Write(new RequestNodesWithPriorityMessage(requestedNodes: 16, CoordinatorBuildPriority.Normal));
        (await ReadServerMessageWithTimeout(normalReader4)).ShouldBeOfType<WaitMessage>();

        using NamedPipeClientStream highClient = await ConnectClientPipeAsync();
        using BinaryWriter highWriter = new(highClient, Encoding.UTF8, leaveOpen: true);
        using BinaryReader highReader = new(highClient, Encoding.UTF8, leaveOpen: true);

        SendHandshake(highWriter, highReader, processId: 10005, capabilities: [Capabilities.Priority]);
        highWriter.Write(new RequestNodesWithPriorityMessage(requestedNodes: 16, CoordinatorBuildPriority.High));
        highReader.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        normalWriter1.Write(ReleaseNodesMessage.Instance);
        (await ReadServerMessageWithTimeout(normalReader4)).ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        normalWriter2.Write(ReleaseNodesMessage.Instance);
        normalWriter3.Write(ReleaseNodesMessage.Instance);
        normalWriter4.Write(ReleaseNodesMessage.Instance);
        highWriter.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task ConcurrentClients_AllReceiveGrants()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope

        // Budget of 16 with 4 clients each requesting 4.
        // All should connect and receive grants without waiting.
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        // Connect all clients concurrently.
        Task<NamedPipeClientStream>[] connectTasks =
        [
            ConnectClientPipeAsync(),
            ConnectClientPipeAsync(),
            ConnectClientPipeAsync(),
            ConnectClientPipeAsync(),
        ];

        NamedPipeClientStream[] clients = await Task.WhenAll(connectTasks);

        try
        {
            // Send handshakes and requests.
            for (int i = 0; i < clients.Length; i++)
            {
                int processId = 20001 + i;
                BinaryWriter w = new(clients[i], Encoding.UTF8, leaveOpen: true);
                BinaryReader r = new(clients[i], Encoding.UTF8, leaveOpen: true);
                SendHandshake(w, r, processId);
                w.Write(new RequestNodesMessage(requestedNodes: 4));
            }

            // All should receive grants (no waits).
            for (int i = 0; i < clients.Length; i++)
            {
                BinaryReader r = new(clients[i], Encoding.UTF8, leaveOpen: true);
                ServerMessage response = r.ReadServerMessage();
                response.ShouldBeOfType<NodeGrantMessage>()
                    .GrantedNodes.ShouldBeGreaterThan(0);
            }
        }
        finally
        {
            foreach (NamedPipeClientStream client in clients)
            {
                BinaryWriter w = new(client, Encoding.UTF8, leaveOpen: true);
                w.Write(ReleaseNodesMessage.Instance);
                client.Dispose();
            }

            _cts.Cancel();

            await serverTask;
        }
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    [Fact]
    public async Task Heartbeat_DoesNotCauseError()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientPipeAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer, reader);
        writer.Write(new RequestNodesMessage(requestedNodes: 4));
        reader.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        // Send several heartbeats. None should cause an error or disconnect.
        writer.Write(HeartbeatMessage.Instance);
        writer.Write(HeartbeatMessage.Instance);
        writer.Write(HeartbeatMessage.Instance);

        // Connection should still be alive — release cleanly.
        writer.Write(ReleaseNodesMessage.Instance);

        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task ClientDisconnect_ReleasesGrant()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client connects, gets grant, then disconnects abruptly.
        NamedPipeClientStream client1 = await ConnectClientPipeAsync();
        BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer1, reader1, processId: 10001);
        writer1.Write(new RequestNodesMessage(requestedNodes: 4));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        // Second client queued.
        using NamedPipeClientStream client2 = await ConnectClientPipeAsync();
        using BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer2, reader2, processId: 10002);
        writer2.Write(new RequestNodesMessage(requestedNodes: 4));
        reader2.ReadServerMessage().ShouldBeOfType<WaitMessage>();

        // Abruptly disconnect first client.
        writer1.Dispose();
        reader1.Dispose();
        client1.Dispose();

        // Second client should eventually receive a grant.
        ServerMessage grantAfterDisconnect = reader2.ReadServerMessage();
        grantAfterDisconnect.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBeGreaterThan(0);

        writer2.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task InvalidFirstMessage_ReceivesError()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientPipeAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        // Send a Heartbeat as the first message (should be rejected).
        writer.Write(HeartbeatMessage.Instance);

        ServerMessage response = reader.ReadServerMessage();
        response.ShouldBeOfType<ErrorMessage>();

        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task InvalidHandshakeProcessId_ReceivesError()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientPipeAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        writer.Write(new ClientHandshakeMessage(Guid.NewGuid(), processId: 0, []));

        reader.ReadServerMessage().ShouldBeOfType<ErrorMessage>();

        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task UnexpectedMessageAfterGrant_ReleasesGrant()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client1 = await ConnectClientPipeAsync();
        using BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer1, reader1, processId: 10001);
        writer1.Write(new RequestNodesMessage(requestedNodes: 4));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        using NamedPipeClientStream client2 = await ConnectClientPipeAsync();
        using BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer2, reader2, processId: 10002);
        writer2.Write(new RequestNodesMessage(requestedNodes: 4));
        reader2.ReadServerMessage().ShouldBeOfType<WaitMessage>();

        writer1.Write(new RequestNodesMessage(requestedNodes: 1));
        reader1.ReadServerMessage().ShouldBeOfType<ErrorMessage>();

        reader2.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBeGreaterThan(0);

        writer2.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task AutoShutdown_ExitsWhenNoBuildsActive()
    {
        using CoordinatorServer server = CreateServer(
            DefaultSettings with
            {
                TotalNodeBudget = 16,
                ShutdownTimeoutMs = 500,
            });

        // Server should shut itself down after 500ms with no clients.
        Task serverTask = server.RunAsync(_cts.Token);

        // Wait for the server to auto-shutdown.
        Task completed = await Task.WhenAny(serverTask, Task.Delay(5000));

        completed.ShouldBeSameAs(serverTask);
    }

    [Fact]
    public async Task SamePidReconnects_CleanlyHandled()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 8);
        Task serverTask = server.RunAsync(_cts.Token);

        int processId = 30001;

        // First connection.
        NamedPipeClientStream client1 = await ConnectClientPipeAsync();
        BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer1, reader1, processId);
        writer1.Write(new RequestNodesMessage(requestedNodes: 4));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        // Disconnect abruptly.
        writer1.Dispose();
        reader1.Dispose();
        client1.Dispose();

        // Reconnect with the same PID. The server will overwrite the old connection
        // entry when it processes the new RequestNodes, and the old connection's
        // finally block will clean up its grant independently.
        using NamedPipeClientStream client2 = await ConnectClientPipeAsync();
        using BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        SendHandshake(writer2, reader2, processId);
        writer2.Write(new RequestNodesMessage(requestedNodes: 4));

        ServerMessage response = reader2.ReadServerMessage();
        response.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBeGreaterThan(0);

        writer2.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();

        await serverTask;
    }

    private async Task<NamedPipeClientStream> ConnectClientPipeAsync()
    {
        NamedPipeClientStream client = new(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        return client;
    }

    /// <summary>
    ///  Performs the handshake and returns the ConnectionId used.
    /// </summary>
    private static ServerHandshakeMessage SendHandshake(
        BinaryWriter writer,
        BinaryReader reader,
        int? processId = null,
        ImmutableArray<string> capabilities = default,
        bool supportsNestedGrants = false)
    {
        var connectionId = Guid.NewGuid();
        ImmutableArray<string> effectiveCapabilities = capabilities.IsDefault
            ? supportsNestedGrants ? [Capabilities.NestedGrants] : []
            : capabilities;

        writer.Write(new ClientHandshakeMessage(connectionId, processId is int pid ? pid : EnvironmentUtilities.CurrentProcessId, effectiveCapabilities));
        return reader.ReadServerMessage().ShouldBeOfType<ServerHandshakeMessage>();
    }

    private static async Task<ServerMessage> ReadServerMessageWithTimeout(BinaryReader reader)
    {
        Task<ServerMessage> readTask = Task.Run(() => reader.ReadServerMessage());
        Task completedTask = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(5)));
        completedTask.ShouldBeSameAs(readTask);
        return await readTask;
    }

    private CoordinatorServer CreateServer(int totalNodeBudget)
        => CreateServer(DefaultSettings with
        {
            TotalNodeBudget = totalNodeBudget,
            HighPriorityReservedNodes = 0,
            MaxNodesPerBuild = 0,
        });

    private CoordinatorServer CreateServer(CoordinatorSettings settings)
        => new(settings, _output);
}
