// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Text;
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

    private readonly TestLogger _logger = new(testOutput);

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    [Fact]
    public async Task SingleClient_ReceivesNodeGrant()
    {
        using CoordinatorServer server = new(totalBudget: 16, _pipeName, shutdownTimeoutMs: Timeout.Infinite, logger: _logger);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        writer.Write(new RequestNodesMessage(requestedNodes: 16, processId: EnvironmentUtilities.CurrentProcessId));

        ServerMessage response = reader.ReadServerMessage();

        response.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(16);

        writer.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task SingleClient_RequestLessThanBudget_GrantsOnlyRequested()
    {
        using CoordinatorServer server = new(totalBudget: 16, _pipeName, shutdownTimeoutMs: Timeout.Infinite, logger: _logger);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        writer.Write(new RequestNodesMessage(requestedNodes: 4, processId: EnvironmentUtilities.CurrentProcessId));

        ServerMessage response = reader.ReadServerMessage();

        response.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        writer.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task MultipleClients_FairShareAllocation()
    {
        using CoordinatorServer server = new(totalBudget: 8, _pipeName, shutdownTimeoutMs: Timeout.Infinite, logger: _logger);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client takes all 8 nodes.
        using NamedPipeClientStream client1 = await ConnectClientAsync();
        using BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        writer1.Write(new RequestNodesMessage(requestedNodes: 8, processId: 10001));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(8);

        // Second client should get a Wait.
        using NamedPipeClientStream client2 = await ConnectClientAsync();
        using BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        writer2.Write(new RequestNodesMessage(requestedNodes: 8, processId: 10002));
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
        using CoordinatorServer server = new(totalBudget: 8, _pipeName, shutdownTimeoutMs: Timeout.Infinite, logger: _logger);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client takes all 8 nodes.
        using NamedPipeClientStream client1 = await ConnectClientAsync();
        using BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        writer1.Write(new RequestNodesMessage(requestedNodes: 8, processId: 10001));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(8);

        // Second client waits.
        using NamedPipeClientStream client2 = await ConnectClientAsync();
        using BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        writer2.Write(new RequestNodesMessage(requestedNodes: 8, processId: 10002));
        reader2.ReadServerMessage().ShouldBeOfType<WaitMessage>();

        // Third client waits.
        using NamedPipeClientStream client3 = await ConnectClientAsync();
        using BinaryWriter writer3 = new(client3, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader3 = new(client3, Encoding.UTF8, leaveOpen: true);

        writer3.Write(new RequestNodesMessage(requestedNodes: 8, processId: 10003));
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
        using CoordinatorServer server = new(totalBudget: 4, _pipeName, shutdownTimeoutMs: Timeout.Infinite, logger: _logger);
        Task serverTask = server.RunAsync(_cts.Token);

        // Active client takes all 4 nodes.
        using NamedPipeClientStream client1 = await ConnectClientAsync();
        using BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        writer1.Write(new RequestNodesMessage(requestedNodes: 4, processId: 10001));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        // First waiter.
        NamedPipeClientStream client2 = await ConnectClientAsync();
        BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        writer2.Write(new RequestNodesMessage(requestedNodes: 4, processId: 10002));
        reader2.ReadServerMessage().ShouldBeOfType<WaitMessage>();

        // Second waiter.
        using NamedPipeClientStream client3 = await ConnectClientAsync();
        using BinaryWriter writer3 = new(client3, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader3 = new(client3, Encoding.UTF8, leaveOpen: true);

        writer3.Write(new RequestNodesMessage(requestedNodes: 4, processId: 10003));
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
    public async Task ConcurrentClients_AllReceiveGrants()
    {
#pragma warning disable CA2000 // Dispose objects before losing scope

        // Budget of 16 with 4 clients each requesting 4.
        // All should connect and receive grants without waiting.
        using CoordinatorServer server = new(totalBudget: 16, _pipeName, shutdownTimeoutMs: Timeout.Infinite, logger: _logger);
        Task serverTask = server.RunAsync(_cts.Token);

        // Connect all clients concurrently.
        Task<NamedPipeClientStream>[] connectTasks =
        [
            ConnectClientAsync(),
            ConnectClientAsync(),
            ConnectClientAsync(),
            ConnectClientAsync(),
        ];

        NamedPipeClientStream[] clients = await Task.WhenAll(connectTasks);

        try
        {
            // Send requests concurrently.
            Task[] requestTasks = new Task[clients.Length];

            for (int i = 0; i < clients.Length; i++)
            {
                int processId = 20001 + i;
                BinaryWriter w = new(clients[i], Encoding.UTF8, leaveOpen: true);
                w.Write(new RequestNodesMessage(requestedNodes: 4, processId: processId));
                requestTasks[i] = Task.CompletedTask;
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
        using CoordinatorServer server = new(totalBudget: 16, _pipeName, shutdownTimeoutMs: Timeout.Infinite, logger: _logger);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientAsync();
        using BinaryWriter writer = new(client, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader = new(client, Encoding.UTF8, leaveOpen: true);

        writer.Write(new RequestNodesMessage(requestedNodes: 4, processId: EnvironmentUtilities.CurrentProcessId));
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
        using CoordinatorServer server = new(totalBudget: 4, _pipeName, shutdownTimeoutMs: Timeout.Infinite, logger: _logger);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client connects, gets grant, then disconnects abruptly.
        NamedPipeClientStream client1 = await ConnectClientAsync();
        BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        writer1.Write(new RequestNodesMessage(requestedNodes: 4, processId: 10001));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        // Second client queued.
        using NamedPipeClientStream client2 = await ConnectClientAsync();
        using BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        writer2.Write(new RequestNodesMessage(requestedNodes: 4, processId: 10002));
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
        using CoordinatorServer server = new(totalBudget: 16, _pipeName, shutdownTimeoutMs: Timeout.Infinite, logger: _logger);
        Task serverTask = server.RunAsync(_cts.Token);

        using NamedPipeClientStream client = await ConnectClientAsync();
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
    public async Task AutoShutdown_ExitsWhenNoBuildsActive()
    {
        using CoordinatorServer server = new(totalBudget: 16, _pipeName, shutdownTimeoutMs: 500, logger: _logger);

        // Server should shut itself down after 500ms with no clients.
        Task serverTask = server.RunAsync(_cts.Token);

        // Wait for the server to auto-shutdown.
        Task completed = await Task.WhenAny(serverTask, Task.Delay(5000));

        completed.ShouldBeSameAs(serverTask);
    }

    [Fact]
    public async Task SamePidReconnects_CleanlyHandled()
    {
        using CoordinatorServer server = new(totalBudget: 8, _pipeName, shutdownTimeoutMs: Timeout.Infinite, logger: _logger);
        Task serverTask = server.RunAsync(_cts.Token);

        int pid = 30001;

        // First connection.
        NamedPipeClientStream client1 = await ConnectClientAsync();
        BinaryWriter writer1 = new(client1, Encoding.UTF8, leaveOpen: true);
        BinaryReader reader1 = new(client1, Encoding.UTF8, leaveOpen: true);

        writer1.Write(new RequestNodesMessage(requestedNodes: 4, processId: pid));
        reader1.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        // Disconnect abruptly.
        writer1.Dispose();
        reader1.Dispose();
        client1.Dispose();

        // Reconnect with the same PID. The server will overwrite the old connection
        // entry when it processes the new RequestNodes, and the old connection's
        // finally block will clean up its grant independently.
        using NamedPipeClientStream client2 = await ConnectClientAsync();
        using BinaryWriter writer2 = new(client2, Encoding.UTF8, leaveOpen: true);
        using BinaryReader reader2 = new(client2, Encoding.UTF8, leaveOpen: true);

        writer2.Write(new RequestNodesMessage(requestedNodes: 4, processId: pid));

        ServerMessage response = reader2.ReadServerMessage();
        response.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBeGreaterThan(0);

        writer2.Write(ReleaseNodesMessage.Instance);
        _cts.Cancel();
        await serverTask;
    }

    private async Task<NamedPipeClientStream> ConnectClientAsync()
    {
        NamedPipeClientStream client = new(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await client.ConnectAsync(5000);
        return client;
    }
}
