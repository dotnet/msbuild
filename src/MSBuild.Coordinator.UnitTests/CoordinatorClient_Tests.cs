// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework.Coordinator;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class CoordinatorClient_Tests : IDisposable
{
    private readonly string _pipeName = $"msbuild-coordinator-test-{Guid.NewGuid():N}";
    private readonly CancellationTokenSource _cts = new();

    // Use fake PIDs that won't collide with each other or the real process.
    // The coordinator server only uses PIDs for keying connections and liveness checks.
    private const int Pid1 = 90001;
    private const int Pid2 = 90002;

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    [Fact]
    public async Task TryConnect_ReceivesNodeGrant()
    {
        using CoordinatorServer server = new(totalBudget: 16, _pipeName, shutdownTimeoutMs: Timeout.Infinite);
        Task serverTask = server.RunAsync(_cts.Token);

        using CoordinatorClient? client = CoordinatorClient.TryConnectToServer(_pipeName, requestedNodes: 8, processId: Pid1);

        client.ShouldNotBeNull();
        client.GrantedNodes.ShouldBe(8);

        client.Dispose();
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task TryConnect_GrantCapsToRequestedNodes()
    {
        using CoordinatorServer server = new(totalBudget: 16, _pipeName, shutdownTimeoutMs: Timeout.Infinite);
        Task serverTask = server.RunAsync(_cts.Token);

        using CoordinatorClient? client = CoordinatorClient.TryConnectToServer(_pipeName, requestedNodes: 4, processId: Pid1);

        client.ShouldNotBeNull();
        client.GrantedNodes.ShouldBe(4);

        client.Dispose();
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task TryConnect_GrantCapsToTotalBudget()
    {
        using CoordinatorServer server = new(totalBudget: 4, _pipeName, shutdownTimeoutMs: Timeout.Infinite);
        Task serverTask = server.RunAsync(_cts.Token);

        using CoordinatorClient? client = CoordinatorClient.TryConnectToServer(_pipeName, requestedNodes: 16, processId: Pid1);

        client.ShouldNotBeNull();
        client.GrantedNodes.ShouldBe(4);

        client.Dispose();
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task TryConnect_NoServer_ReturnsNull()
    {
        // Use a pipe name that no server is listening on.
        CoordinatorClient? client = CoordinatorClient.TryConnectToServer(
            $"msbuild-coordinator-nonexistent-{Guid.NewGuid():N}",
            requestedNodes: 8,
            processId: Pid1,
            connectionTimeoutMs: 500);

        client.ShouldBeNull();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task Dispose_ReleasesGrant_SecondClientGetsNodes()
    {
        using CoordinatorServer server = new(totalBudget: 4, _pipeName, shutdownTimeoutMs: Timeout.Infinite);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client takes the full budget.
        CoordinatorClient? client1 = CoordinatorClient.TryConnectToServer(_pipeName, requestedNodes: 4, processId: Pid1);
        client1.ShouldNotBeNull();
        client1.GrantedNodes.ShouldBe(4);

        // Second client will be queued (different PID so the server tracks it separately).
        Task<CoordinatorClient?> client2Task = Task.Run(() =>
            CoordinatorClient.TryConnectToServer(_pipeName, requestedNodes: 4, processId: Pid2));

        // Give the second client time to connect and be queued.
        await Task.Delay(200);

        // Release the first client's grant. This should unblock the second client.
        client1.Dispose();

        CoordinatorClient? client2 = await client2Task;
        client2.ShouldNotBeNull();
        client2.GrantedNodes.ShouldBeGreaterThan(0);

        client2.Dispose();
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task Dispose_SendsReleaseMessage()
    {
        using CoordinatorServer server = new(totalBudget: 8, _pipeName, shutdownTimeoutMs: Timeout.Infinite);
        Task serverTask = server.RunAsync(_cts.Token);

        CoordinatorClient? client = CoordinatorClient.TryConnectToServer(_pipeName, requestedNodes: 8, processId: Pid1);
        client.ShouldNotBeNull();
        client.GrantedNodes.ShouldBe(8);

        // Dispose sends ReleaseNodesMessage. Verify it doesn't throw.
        client.Dispose();

        // Disposing again should be a no-op.
        client.Dispose();

        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task MultipleClients_FairShare()
    {
        using CoordinatorServer server = new(totalBudget: 8, _pipeName, shutdownTimeoutMs: Timeout.Infinite);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client connects and gets all 8.
        CoordinatorClient? client1 = CoordinatorClient.TryConnectToServer(_pipeName, requestedNodes: 8, processId: Pid1);
        client1.ShouldNotBeNull();
        client1.GrantedNodes.ShouldBe(8);

        // Release first client so second can get nodes.
        client1.Dispose();

        // Second client connects and should also get up to 8.
        using CoordinatorClient? client2 = CoordinatorClient.TryConnectToServer(_pipeName, requestedNodes: 8, processId: Pid2);
        client2.ShouldNotBeNull();
        client2.GrantedNodes.ShouldBe(8);

        client2.Dispose();
        _cts.Cancel();
        await serverTask;
    }
}
