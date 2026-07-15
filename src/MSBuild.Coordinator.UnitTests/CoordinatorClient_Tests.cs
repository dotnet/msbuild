// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class CoordinatorClient_Tests(ITestOutputHelper testOutput) : IDisposable
{
    // Use fake PIDs that won't collide with each other or the real process.
    // The coordinator server only uses PIDs for keying connections and liveness checks.
    private const int Pid1 = 90001;
    private const int Pid2 = 90002;

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
    public Task TryConnect_ReceivesNodeGrant()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using CoordinatorClient? client = TryConnectToServer(requestedNodes: 8, processId: Pid1);

        client.ShouldNotBeNull();
        client.GrantedNodes.ShouldBe(8);

        client.Dispose();
        _cts.Cancel();

        return serverTask;
    }

    [Fact]
    public Task TryConnect_GrantCapsToRequestedNodes()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using CoordinatorClient? client = TryConnectToServer(requestedNodes: 4, processId: Pid1);

        client.ShouldNotBeNull();
        client.GrantedNodes.ShouldBe(4);

        client.Dispose();
        _cts.Cancel();

        return serverTask;
    }

    [Fact]
    public Task TryConnect_GrantCapsToTotalBudget()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        using CoordinatorClient? client = TryConnectToServer(requestedNodes: 16, processId: Pid1);

        client.ShouldNotBeNull();
        client.GrantedNodes.ShouldBe(4);

        client.Dispose();
        _cts.Cancel();

        return serverTask;
    }

    [Fact]
    public Task TryConnect_RootGrant_DoesNotMutateProcessEnvironment()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        env.SetEnvironmentVariable(Constants.GrantIdEnvVarName, null);

        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        CoordinatorClient? client = TryConnectToServer(requestedNodes: 8, processId: Pid1);
        client.ShouldNotBeNull();
        client.GrantId.ShouldNotBe(Guid.Empty);
        Environment.GetEnvironmentVariable(Constants.GrantIdEnvVarName).ShouldBeNull();

        client.Dispose();
        Environment.GetEnvironmentVariable(Constants.GrantIdEnvVarName).ShouldBeNull();

        _cts.Cancel();

        return serverTask;
    }

    [Fact]
    public Task TryConnect_WithInheritedGrantId_JoinsExistingGrant()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        env.SetEnvironmentVariable(Constants.GrantIdEnvVarName, null);

        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        CoordinatorClient? rootClient = TryConnectToServer(requestedNodes: 4, processId: Pid1);
        rootClient.ShouldNotBeNull();
        rootClient.GrantedNodes.ShouldBe(4);
        rootClient.GrantId.ShouldNotBe(Guid.Empty);

        env.SetEnvironmentVariable(Constants.GrantIdEnvVarName, rootClient.GrantId.ToString("D"));

        using CoordinatorClient? nestedClient = TryConnectToServer(requestedNodes: 8, processId: Pid2);
        nestedClient.ShouldNotBeNull();
        nestedClient.GrantedNodes.ShouldBe(4);
        nestedClient.GrantId.ShouldBe(rootClient.GrantId);

        nestedClient.Dispose();
        Environment.GetEnvironmentVariable(Constants.GrantIdEnvVarName).ShouldBe(rootClient.GrantId.ToString("D"));

        rootClient.Dispose();
        Environment.GetEnvironmentVariable(Constants.GrantIdEnvVarName).ShouldBe(rootClient.GrantId.ToString("D"));
        _cts.Cancel();

        return serverTask;
    }

    [Fact]
    public void TryConnect_NoServer_ReturnsNull()
    {
        // Use a pipe name that no server is listening on.
        CoordinatorClient? client = TryConnectToServer(
            requestedNodes: 8,
            CoordinatorSettings.Default with
            {
                PipeName = NamedPipeUtil.GetPlatformSpecificPipeName($"msbuild-coordinator-nonexistent-{Guid.NewGuid():N}"),
                ProcessId = Pid1,
                ConnectionTimeoutMs = 500,
            });

        client.ShouldBeNull();
    }

    [Fact]
    public async Task TryConnect_WithInheritedGrantIdAndServerWithoutNestedGrantCapability_RequestsRootGrant()
    {
        using TestEnvironment env = TestEnvironment.Create(testOutput);
        env.SetEnvironmentVariable(Constants.GrantIdEnvVarName, Guid.NewGuid().ToString("D"));

        using NamedPipeServerStream serverPipe = new(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        Task serverTask = Task.Run(async () =>
        {
            await serverPipe.WaitForConnectionAsync(_cts.Token);

            using BinaryReader reader = new(serverPipe, System.Text.Encoding.UTF8, leaveOpen: true);
            using BinaryWriter writer = new(serverPipe, System.Text.Encoding.UTF8, leaveOpen: true);

            reader.ReadClientMessage().ShouldBeOfType<ClientHandshakeMessage>()
                .Capabilities.ShouldContain(Capabilities.NestedGrants);

            writer.Write(new ServerHandshakeMessage([]));

            reader.ReadClientMessage().ShouldBe(new RequestNodesMessage(requestedNodes: 8));
            writer.Write(new NodeGrantMessage(grantedNodes: 3));
            reader.ReadClientMessage().ShouldBe(ReleaseNodesMessage.Instance);
        });

        CoordinatorClient? client = TryConnectToServer(
            requestedNodes: 8,
            DefaultSettings with
            {
                ProcessId = Pid1,
            });

        client.ShouldNotBeNull();
        client.GrantedNodes.ShouldBe(3);
        client.GrantId.ShouldBe(Guid.Empty);

        client.Dispose();
        await serverTask;
    }

    [Fact]
    public Task TryConnect_CustomSettings_UsesSettingsPipeName()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 16);
        Task serverTask = server.RunAsync(_cts.Token);

        using CoordinatorClient? client = TryConnectToServer(
            requestedNodes: 8,
            DefaultSettings with
            {
                ProcessId = Pid1,
                HeartbeatIntervalMs = 50,
            });

        client.ShouldNotBeNull();
        client.GrantedNodes.ShouldBe(8);

        client.Dispose();
        _cts.Cancel();

        return serverTask;
    }

    [Fact]
    public async Task Dispose_ReleasesGrant_SecondClientGetsNodes()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 4);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client takes the full budget.
        CoordinatorClient? client1 = TryConnectToServer(requestedNodes: 4, processId: Pid1);
        client1.ShouldNotBeNull();
        client1.GrantedNodes.ShouldBe(4);

        // Second client will be queued (different PID so the server tracks it separately).
        Task<CoordinatorClient?> client2Task = Task.Run(() =>
            TryConnectToServer(requestedNodes: 4, processId: Pid2));

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
    public Task MultipleClients_SequentialReuse()
    {
        using CoordinatorServer server = CreateServer(totalNodeBudget: 8);
        Task serverTask = server.RunAsync(_cts.Token);

        // First client connects and gets all 8.
        CoordinatorClient? client1 = TryConnectToServer(requestedNodes: 8, processId: Pid1);
        client1.ShouldNotBeNull();
        client1.GrantedNodes.ShouldBe(8);

        // Release first client so second can get nodes.
        client1.Dispose();

        // Second client connects and should also get up to 8.
        using CoordinatorClient? client2 = TryConnectToServer(requestedNodes: 8, processId: Pid2);
        client2.ShouldNotBeNull();
        client2.GrantedNodes.ShouldBe(8);

        client2.Dispose();
        _cts.Cancel();

        return serverTask;
    }

    private CoordinatorServer CreateServer(int totalNodeBudget)
        => new(DefaultSettings with { TotalNodeBudget = totalNodeBudget }, _output);

    private CoordinatorClient? TryConnectToServer(int requestedNodes, int processId)
        => TryConnectToServer(requestedNodes, DefaultSettings with { ProcessId = processId });

    private CoordinatorClient? TryConnectToServer(int requestedNodes, CoordinatorSettings settings)
        => CoordinatorClient.TestAccessor.TryConnectToServer(requestedNodes, settings, _output);
}
