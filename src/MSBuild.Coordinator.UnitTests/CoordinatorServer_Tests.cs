// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework.Coordinator;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class CoordinatorServer_Tests(ITestOutputHelper testOutput)
{
    private readonly string _pipeName = NamedPipeUtil.GetPlatformSpecificPipeName($"msbuild-coordinator-test-{Guid.NewGuid():N}");

    private CoordinatorSettings DefaultSettings => CoordinatorSettings.Default with
    {
        PipeName = _pipeName,
        ShutdownTimeoutMs = Timeout.Infinite,
    };

    [Fact]
    public Task SingleClient_ReceivesNodeGrant()
        => CreateAndRunServerAsync(totalNodeBudget: 16, async () =>
        {
            using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName);
            ServerMessage response = client.RequestNodes(requestedNodes: 16);

            response.ShouldBeOfType<NodeGrantMessage>()
                .GrantedNodes.ShouldBe(16);

            client.ReleaseNodes();
        });

    [Fact]
    public Task SingleClient_RequestLessThanBudget_GrantsOnlyRequested()
        => CreateAndRunServerAsync(totalNodeBudget: 16, async () =>
        {
            using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName);
            ServerMessage response = client.RequestNodes(requestedNodes: 4);

            response.ShouldBeOfType<NodeGrantMessage>()
                .GrantedNodes.ShouldBe(4);

            client.ReleaseNodes();
        });

    [Fact]
    public Task ClientWithNestedGrantCapability_ReceivesGrantId()
        => CreateAndRunServerAsync(totalNodeBudget: 4, async () =>
        {
            using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName, supportsNestedGrants: true);
            NodeGrantMessage response = client.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();
            response.GrantedNodes.ShouldBe(4);
            response.GrantId.ShouldNotBe(Guid.Empty);

            client.ReleaseNodes();
        });

    [Fact]
    public Task ClientWithoutNestedGrantCapability_ReceivesLegacyNodeGrant()
        => CreateAndRunServerAsync(totalNodeBudget: 4, async () =>
        {
            using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName, supportsNestedGrants: false);
            client.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>()
                .GrantedNodes.ShouldBe(4);

            client.ReleaseNodes();
        });

    [Fact]
    public Task JoinGrantWithoutNestedGrantCapability_ReceivesError()
        => CreateAndRunServerAsync(totalNodeBudget: 4, async () =>
        {
            using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName, supportsNestedGrants: false);
            client.JoinGrant(Guid.NewGuid(), requestedNodes: 4).ShouldBeOfType<ErrorMessage>();
        });

    [Fact]
    public Task NestedClient_JoinsRootGrant_WithoutConsumingAdditionalBudget()
        => CreateAndRunServerAsync(totalNodeBudget: 4, async () =>
        {
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
        });

    [Fact]
    public Task MultipleClients_FairShareAllocation()
        => CreateAndRunServerAsync(totalNodeBudget: 8, async () =>
        {
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
        });

    [Fact]
    public Task MultipleWaiters_FairShareDistribution()
        => CreateAndRunServerAsync(totalNodeBudget: 8, async () =>
        {
            // Budget of 8. One active client holds all 8. Two clients wait.
            // When the active client releases, both waiters should get fair-share grants.

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
        });

    [Fact]
    public Task WaitingClientDisconnects_DoesNotAffectOtherWaiters()
        => CreateAndRunServerAsync(totalNodeBudget: 4, async () =>
        {
            // Budget of 4. One active client holds all 4. Two clients wait.
            // The first waiter disconnects. When the active client releases,
            // the remaining waiter should get a grant.

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
        });

    [Fact]
    [ActiveIssue("https://github.com/dotnet/msbuild/issues/14193", TestPlatforms.Windows)]
    public Task ConcurrentClients_AllReceiveGrants()
        => CreateAndRunServerAsync(totalNodeBudget: 16, async () =>
        {
            // Budget of 16 with 4 clients each requesting 4.
            // All should connect and receive grants without waiting.

            // Connect and handshake all clients concurrently so accepted server connections
            // can progress while the accept loop creates the remaining pipe instances.
#pragma warning disable CA2000 // Dispose objects before losing scope
            Task<TestCoordinatorClient>[] connectTasks =
            [
                TestCoordinatorClient.ConnectAsync(_pipeName, processId: 20001),
                TestCoordinatorClient.ConnectAsync(_pipeName, processId: 20002),
                TestCoordinatorClient.ConnectAsync(_pipeName, processId: 20003),
                TestCoordinatorClient.ConnectAsync(_pipeName, processId: 20004),
            ];
#pragma warning restore CA2000 // Dispose objects before losing scope

            TestCoordinatorClient[] clients = await Task.WhenAll(connectTasks);

            try
            {
                foreach (TestCoordinatorClient client in clients)
                {
                    client.SendRequestNodes(requestedNodes: 4);
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
            }
        });

    [Fact]
    public Task Heartbeat_DoesNotCauseError()
        => CreateAndRunServerAsync(totalNodeBudget: 16, async () =>
        {
            using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName);
            client.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();

            // Send several heartbeats. None should cause an error or disconnect.
            client.SendHeartbeat();
            client.SendHeartbeat();
            client.SendHeartbeat();

            // Connection should still be alive — release cleanly.
            client.ReleaseNodes();
        });

    [Fact]
    public Task ClientDisconnect_ReleasesGrant()
        => CreateAndRunServerAsync(totalNodeBudget: 4, async () =>
        {
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
        });

    [Fact]
    public Task InvalidFirstMessage_ReceivesError()
        => CreateAndRunServerAsync(totalNodeBudget: 16, async () =>
        {
            // Send a Heartbeat as the first message (should be rejected).
            using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName, sendHandshake: false);
            client.SendHeartbeat();
            ServerMessage response = client.ReadServerMessage();
            response.ShouldBeOfType<ErrorMessage>();
        });

    [Fact]
    public Task InvalidHandshakeProcessId_ReceivesError()
        => CreateAndRunServerAsync(totalNodeBudget: 16, async () =>
        {
            using TestCoordinatorClient client = await TestCoordinatorClient.ConnectAsync(_pipeName, sendHandshake: false);
            client.Send(new ClientHandshakeMessage(Guid.NewGuid(), processId: 0, []));
            client.ReadServerMessage().ShouldBeOfType<ErrorMessage>();
        });

    [Fact]
    public Task UnexpectedMessageAfterGrant_ReleasesGrant()
        => CreateAndRunServerAsync(totalNodeBudget: 4, async () =>
        {
            using TestCoordinatorClient client1 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10001);
            client1.RequestNodes(requestedNodes: 4).ShouldBeOfType<NodeGrantMessage>();

            using TestCoordinatorClient client2 = await TestCoordinatorClient.ConnectAsync(_pipeName, processId: 10002);
            client2.RequestNodes(requestedNodes: 4).ShouldBeOfType<WaitMessage>();

            client1.RequestNodes(requestedNodes: 1).ShouldBeOfType<ErrorMessage>();

            client2.ReadServerMessage().ShouldBeOfType<NodeGrantMessage>()
                .GrantedNodes.ShouldBeGreaterThan(0);

            client2.ReleaseNodes();
        });

    [Fact]
    public async Task AutoShutdown_ExitsWhenNoBuildsActive()
    {
        // CreateAndRunServerAsync cancels the server after its test callback. This test
        // instead leaves the server uncancelled to verify that its idle timer stops it.
        using CoordinatorServer server = CreateServer(
            DefaultSettings with
            {
                TotalNodeBudget = 16,
                ShutdownTimeoutMs = 500,
            });

        // Server should shut itself down after 500ms with no clients.
        Task serverTask = server.RunAsync(CancellationToken.None);

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

        var settings = DefaultSettings with
        {
            ShutdownTimeoutMs = 1,
        };

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

        // This test controls the exact point of disposal, so it uses the non-owning
        // RunServerAsync helper and disposes the server from the test callback.
#pragma warning disable CA2000 // Dispose objects before losing scope
        CoordinatorServer server = CreateServer(settings, output);
#pragma warning restore CA2000 // Dispose objects before losing scope

        bool callbackStartedBeforeTimeout = false;
        bool callbackCompletedBeforeDisposeReturned = false;

        await RunServerAsync(server, () =>
        {
            callbackStartedBeforeTimeout = callbackStarted.Wait(TimeSpan.FromSeconds(5));

            Task releaseTask = Task.Run(
                async () =>
                {
                    disposeStarted.Wait();
                    await Task.Delay(200);
                    releaseCallback.Set();
                });

            disposeStarted.Set();
            server.Dispose();
            callbackCompletedBeforeDisposeReturned = callbackCompleted.IsSet;

            return releaseTask;
        });

        callbackStartedBeforeTimeout.ShouldBeTrue();
        callbackCompletedBeforeDisposeReturned.ShouldBeTrue();
    }

    [Fact]
    [ActiveIssue("https://github.com/dotnet/msbuild/issues/15341", TestPlatforms.Linux)]
    public Task SamePidReconnects_CleanlyHandled()
        => CreateAndRunServerAsync(totalNodeBudget: 8, async () =>
        {
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
        });

    /// <summary>
    ///  Creates a coordinator server with the specified node budget and default test settings.
    /// </summary>
    /// <param name="totalNodeBudget">The total node count available to connected clients.</param>
    /// <returns>
    ///  The coordinator server.
    /// </returns>
    private CoordinatorServer CreateServer(int totalNodeBudget)
        => CreateServer(DefaultSettings with { TotalNodeBudget = totalNodeBudget });

    /// <summary>
    ///  Creates a coordinator server that writes diagnostic output to the current test.
    /// </summary>
    /// <param name="settings">The coordinator settings.</param>
    /// <returns>
    ///  The coordinator server.
    /// </returns>
    private CoordinatorServer CreateServer(CoordinatorSettings settings)
        => new(settings, new TestCoordinatorDebugOutput(testOutput));

    /// <summary>
    ///  Creates a coordinator server with the specified settings and diagnostic output.
    /// </summary>
    /// <param name="settings">The coordinator settings.</param>
    /// <param name="output">The diagnostic output used by the server.</param>
    /// <returns>
    ///  The coordinator server.
    /// </returns>
    private CoordinatorServer CreateServer(CoordinatorSettings settings, ICoordinatorDebugOutput output)
        => new(settings, output);

    /// <summary>
    ///  Creates, runs, stops, and disposes a coordinator server around the supplied test code.
    /// </summary>
    /// <param name="totalNodeBudget">The total node count available to connected clients.</param>
    /// <param name="testCode">The test operation to run after the server starts.</param>
    /// <returns>
    ///  A task that completes after the test operation and server shutdown finish.
    /// </returns>
    private async Task CreateAndRunServerAsync(int totalNodeBudget, Func<Task> testCode)
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget);
        await RunServerAsync(server, testCode);
    }

    /// <summary>
    ///  Runs test code against an existing coordinator server, then cancels and awaits the server.
    /// </summary>
    /// <param name="server">The externally owned coordinator server.</param>
    /// <param name="testCode">The test operation to run after the server starts.</param>
    /// <returns>
    ///  A task that completes after the test operation and server shutdown finish.
    /// </returns>
    /// <remarks>
    ///  This method does not dispose <paramref name="server"/>. The caller retains ownership so tests
    ///  that validate disposal can control the exact point at which disposal occurs.
    /// </remarks>
    private async Task RunServerAsync(CoordinatorServer server, Func<Task> testCode)
    {
        using CancellationTokenSource cts = new();

        // Start the accept loop before the test connects any clients.
        Task serverTask = server.RunAsync(cts.Token);

        try
        {
            await testCode();
        }
        finally
        {
            // Always stop and await the server so no background work can write to
            // the test output after the test operation has completed.
            cts.Cancel();

            await serverTask;
        }
    }
}
