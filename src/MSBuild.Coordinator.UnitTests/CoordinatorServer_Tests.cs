// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName);
        ServerMessage response = client.RequestNodes(requestedNodes: 16);

        response.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(16);

        client.ReleaseNodes();
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task SingleClient_RequestLessThanBudget_GrantsOnlyRequested()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName);
        ServerMessage response = client.RequestNodes(requestedNodes: 4);

        response.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        client.ReleaseNodes();
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task ClientWithNestedGrantCapability_ReceivesGrantId()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName, supportsNestedGrants: true);
        NodeGrantMessage response = client.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();
        response.GrantedNodes.ShouldBe(4);
        response.GrantId.ShouldNotBe(Guid.Empty);

        client.ReleaseNodes();
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task ClientWithoutNestedGrantCapability_ReceivesLegacyNodeGrant()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName, supportsNestedGrants: false);
        client.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(4);

        client.ReleaseNodes();
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task JoinGrantWithoutNestedGrantCapability_ReceivesError()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName, supportsNestedGrants: false);
        client.JoinGrant(Guid.NewGuid(), requestedNodes: 4).ShouldBeOfType<ErrorMessage>();

        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task NestedClient_JoinsRootGrant_WithoutConsumingAdditionalBudget()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using TestCoordinatorClient rootClient = await TestCoordinatorClient.ConnectAsync(
            _pipeName,
            processId: 10001,
            supportsNestedGrants: true);
        NodeGrantMessage rootGrant = rootClient.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();
        rootGrant.GrantedNodes.ShouldBe(4);

        using TestCoordinatorClient nestedClient = await TestCoordinatorClient.ConnectAsync(
            _pipeName,
            processId: 10002,
            supportsNestedGrants: true);
        NodeGrantMessage nestedGrant = nestedClient.JoinGrant(rootGrant.GrantId, requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();
        nestedGrant.GrantedNodes.ShouldBe(4);
        nestedGrant.GrantId.ShouldBe(rootGrant.GrantId);

        using TestCoordinatorClient unrelatedClient = await TestCoordinatorClient.ConnectAsync(
            _pipeName,
            processId: 10003,
            supportsNestedGrants: true);
        unrelatedClient.RequestNodes(requestedNodes: 4).ShouldBeOfType<WaitMessage>();

        nestedClient.ReleaseNodes();
        Task<ServerMessage> unrelatedGrantTask = Task.Run(unrelatedClient.ReadServerMessage);
        Task completedBeforeRootRelease = await Task.WhenAny(unrelatedGrantTask, Task.Delay(200));
        completedBeforeRootRelease.ShouldNotBeSameAs(unrelatedGrantTask);

        rootClient.ReleaseNodes();
        (await unrelatedGrantTask).ShouldBeOfType<NodeGrantMessage>();

        unrelatedClient.ReleaseNodes();
        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task MultipleClients_FairShareAllocation()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 8);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client takes all 8 nodes.
        using TestCoordinatorClient client1 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10001);
        client1.RequestNodes(requestedNodes: 8).ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(8);

        // Second client should get a Wait.
        using TestCoordinatorClient client2 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10002);
        client2.RequestNodes(requestedNodes: 8).ShouldBeOfType<WaitMessage>();

        // First client releases. Second client should receive a NodeGrant.
        client1.ReleaseNodes();

        NodeGrantMessage grantAfterWait = client2.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();
        grantAfterWait.GrantedNodes.ShouldBeGreaterThan(0);

        client2.ReleaseNodes();
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
        using TestCoordinatorClient client1 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10001);
        client1.RequestNodes(requestedNodes: 8).ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBe(8);

        // Second client waits.
        using TestCoordinatorClient client2 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10002);
        client2.RequestNodes(requestedNodes: 8).ShouldBeOfType<WaitMessage>();

        // Third client waits.
        using TestCoordinatorClient client3 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10003);
        client3.RequestNodes(requestedNodes: 8).ShouldBeOfType<WaitMessage>();

        // Release first client. Both waiters should get grants.
        client1.ReleaseNodes();

        NodeGrantMessage grant2 = client2.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();
        NodeGrantMessage grant3 = client3.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>();

        // Both should get nodes, and together they shouldn't exceed budget.
        grant2.GrantedNodes.ShouldBeGreaterThan(0);
        grant3.GrantedNodes.ShouldBeGreaterThan(0);
        (grant2.GrantedNodes + grant3.GrantedNodes).ShouldBeLessThanOrEqualTo(8);

        client2.ReleaseNodes();
        client3.ReleaseNodes();
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
        using TestCoordinatorClient client1 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10001);
        client1.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();

        // First waiter.
        TestCoordinatorClient client2 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10002);
        client2.RequestNodes(requestedNodes: 4).ShouldBeOfType<WaitMessage>();

        // Second waiter.
        using TestCoordinatorClient client3 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10003);
        client3.RequestNodes(requestedNodes: 4).ShouldBeOfType<WaitMessage>();

        // First waiter disconnects abruptly.
        client2.Dispose();

        // Active client releases. The server will attempt to grant the disconnected
        // waiter, fail with IOException, and then grant the remaining waiter.
        client1.ReleaseNodes();

        ServerMessage grant = client3.ReadServerMessage();
        grant.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBeGreaterThan(0);

        client3.ReleaseNodes();
        _cts.Cancel();

        await serverTask;
    }

    [ActiveIssue("https://github.com/dotnet/msbuild/issues/14193", TestPlatforms.Windows)]
    [Fact]
    public async Task ConcurrentClients_AllReceiveGrants()
    {
        // Budget of 16 with 4 clients each requesting 4.
        // All should connect and receive grants without waiting.
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        // Connect all clients concurrently.
#pragma warning disable CA2000 // Dispose objects before losing scope
        Task<TestCoordinatorClient>[] connectTasks =
        [
            TestCoordinatorClient.ConnectAsync(_pipeName, sendHandshake: false),
            TestCoordinatorClient.ConnectAsync(_pipeName, sendHandshake: false),
            TestCoordinatorClient.ConnectAsync(_pipeName, sendHandshake: false),
            TestCoordinatorClient.ConnectAsync(_pipeName, sendHandshake: false),
        ];
#pragma warning restore CA2000 // Dispose objects before losing scope

        TestCoordinatorClient[] clients = await Task.WhenAll(connectTasks);

        try
        {
            for (int i = 0; i < clients.Length; i++)
            {
                clients[i].Handshake(processId: 20001 + i);
                clients[i].SendRequestNodes(requestedNodes: 4);
            }

            foreach (TestCoordinatorClient client in clients)
            {
                client.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
                    .GrantedNodes.ShouldBeGreaterThan(0);
            }
        }
        finally
        {
            foreach (TestCoordinatorClient client in clients)
            {
                client.ReleaseNodes();
                client.Dispose();
            }

            _cts.Cancel();
            await serverTask;
        }
    }

    [Fact]
    public async Task Heartbeat_DoesNotCauseError()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName);
        client.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();

        // Send several heartbeats. None should cause an error or disconnect.
        client.SendHeartbeat();
        client.SendHeartbeat();
        client.SendHeartbeat();

        // Connection should still be alive — release cleanly.
        client.ReleaseNodes();

        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task ClientDisconnect_ReleasesGrant()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client connects, gets grant, then disconnects abruptly.
        TestCoordinatorClient client1 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10001);
        client1.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();

        // Second client queued.
        using TestCoordinatorClient client2 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10002);
        client2.RequestNodes(requestedNodes: 4).ShouldBeOfType<WaitMessage>();

        // Abruptly disconnect first client.
        client1.Dispose();

        // Second client should eventually receive a grant.
        ServerMessage grantAfterDisconnect = client2.ReadServerMessage();
        grantAfterDisconnect.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBeGreaterThan(0);

        client2.ReleaseNodes();
        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task InvalidFirstMessage_ReceivesError()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        // Send a Heartbeat as the first message (should be rejected).
        using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName, sendHandshake: false);
        client.SendHeartbeat();
        ServerMessage response = client.ReadServerMessage();
        response.ShouldBeOfType<ErrorMessage>();

        _cts.Cancel();

        await serverTask;
    }

    [Fact]
    public async Task InvalidHandshakeProcessId_ReceivesError()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName, sendHandshake: false);
        client.Send(new ClientHandshakeMessage(Guid.NewGuid(), processId: 0, []));
        client.ReadServerMessage().ShouldBeOfType<ErrorMessage>();

        _cts.Cancel();
        await serverTask;
    }

    [Fact]
    public async Task UnexpectedMessageAfterGrant_ReleasesGrant()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using TestCoordinatorClient client1 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10001);
        client1.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();

        using TestCoordinatorClient client2 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10002);
        client2.RequestNodes(requestedNodes: 4).ShouldBeOfType<WaitMessage>();

        client1.RequestNodes(requestedNodes: 1).ShouldBeOfType<ErrorMessage>();

        client2.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBeGreaterThan(0);

        client2.ReleaseNodes();
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
    public async Task Dispose_WaitsForInFlightShutdownCallback()
    {
        using ManualResetEventSlim callbackStarted = new();
        using ManualResetEventSlim releaseCallback = new();
        using ManualResetEventSlim callbackCompleted = new();
        using ManualResetEventSlim disposeStarted = new();

        var output = new TestCoordinatorDebugOutput(
            testOutput,
            message =>
            {
                if (message == "CoordinatorServer: Auto-shutdown (no active or waiting builds)")
                {
                    callbackStarted.Set();
                    releaseCallback.Wait();
                    callbackCompleted.Set();
                }
            });

        CoordinatorServer server = new(
            DefaultSettings with
            {
                ShutdownTimeoutMs = 1,
            },
            output);

        Task serverTask = server.RunAsync(_cts.Token);
        bool callbackStartedBeforeTimeout = callbackStarted.Wait(TimeSpan.FromSeconds(5));
        Task releaseTask = Task.Run(
            async () =>
            {
                disposeStarted.Wait();
                await Task.Delay(200);
                releaseCallback.Set();
            });

        disposeStarted.Set();
        server.Dispose();
        bool callbackCompletedBeforeDisposeReturned = callbackCompleted.IsSet;

        await releaseTask;
        await serverTask;

        callbackStartedBeforeTimeout.ShouldBeTrue();
        callbackCompletedBeforeDisposeReturned.ShouldBeTrue();
    }

    [ActiveIssue("https://github.com/dotnet/msbuild/issues/15341", TestPlatforms.Linux)]
    [Fact]
    public async Task SamePidReconnects_CleanlyHandled()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 8);
        Task serverTask = server.RunAsync(_cts.Token);

        int processId = 30001;

        // First connection.
        TestCoordinatorClient client1 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId);
        client1.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();

        // Disconnect abruptly.
        client1.Dispose();

        // Reconnect with the same PID. The server will overwrite the old connection
        // entry when it processes the new RequestNodes, and the old connection's
        // finally block will clean up its grant independently.
        using TestCoordinatorClient client2 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId);
        ServerMessage response = client2.RequestNodes(requestedNodes: 4);
        response.ShouldBeOfType<NodeGrantMessage>()
            .GrantedNodes.ShouldBeGreaterThan(0);

        client2.ReleaseNodes();
        _cts.Cancel();

        await serverTask;
    }

    private CoordinatorServer CreateServer(int totalNodeBudget)
        => CreateServer(DefaultSettings with { TotalNodeBudget = totalNodeBudget });

    private CoordinatorServer CreateServer(CoordinatorSettings settings)
        => new(settings, _output);
}
