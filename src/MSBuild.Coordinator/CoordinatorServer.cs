// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  The coordinator server that listens for MSBuild client connections on a named pipe,
///  manages node grants, and monitors build health via heartbeats.
/// </summary>
/// <param name="settings">Configuration for the coordinator (pipe name, budget, timeouts, etc.).</param>
/// <param name="output">Optional debug trace output. Defaults to file-based trace logging gated on MSBUILDDEBUGCOMM.</param>
internal sealed partial class CoordinatorServer(CoordinatorSettings settings, ICoordinatorDebugOutput? output = null) : IDisposable
{
    private readonly CoordinatorSettings _settings = settings;
    private readonly NodeBudgetManager _budgetManager = new(settings.TotalNodeBudget);
    private readonly string _pipeName = settings.PipeName;
    private readonly int _heartbeatIntervalMs = settings.HeartbeatIntervalMs;
    private readonly int _shutdownTimeoutMs = settings.ShutdownTimeoutMs;
    private readonly Dictionary<Guid, ConnectedClient> _clientsById = [];
    private readonly Dictionary<Guid, BuildGrant> _activeRootGrantsById = [];
    private readonly ReaderWriterLockSlim _clientsLock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ICoordinatorDebugOutput _output = output ?? DefaultDebugOutput.Instance;
    private Timer? _heartbeatMonitor;
    private Timer? _shutdownTimer;
    private int _activeConnections;

    private bool HasActiveConnections => Volatile.Read(ref _activeConnections) > 0;

    private bool IsIdle => _budgetManager.IsIdle && !HasActiveConnections;

    public void Dispose()
    {
        _cts.Cancel();
        _heartbeatMonitor?.Dispose();
        _shutdownTimer?.Dispose();
        _cts.Dispose();
        _clientsLock.Dispose();
    }

    /// <summary>
    ///  Runs the coordinator server until cancellation is requested or the auto-shutdown
    ///  timeout elapses with no active or waiting builds.
    /// </summary>
    /// <param name="cancellationToken">Token to signal the server should stop accepting connections.</param>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
        CancellationToken token = linked.Token;

        // Start heartbeat monitoring.
        _heartbeatMonitor = new Timer(
            CheckHeartbeats,
            state: null,
            dueTime: _heartbeatIntervalMs,
            period: _heartbeatIntervalMs);

        // Start auto-shutdown timer.
        ResetShutdownTimer();

        _output.WriteLine($"CoordinatorServer: Accept loop started on pipe '{_pipeName}' (budget={_settings.TotalNodeBudget})");

        ConcurrentDictionary<Task, byte> clientTasks = [];

        try
        {
            while (!token.IsCancellationRequested)
            {
                NamedPipeServerStream? pipeStream = await WaitForClientAsync(token);

                if (pipeStream is null)
                {
                    break;
                }

                // Dispatch client handling to the thread pool so the accept loop immediately
                // creates the next pipe instance. This is necessary because HandleClientAsync
                // performs a synchronous read (ClientMessage.Read) before its first await,
                // which would block the accept loop if run inline.
                //
                // CancellationToken.None is intentional: we don't want Task.Run to cancel
                // before HandleClientAsync starts, which would orphan the pipe stream.
                // HandleClientAsync receives the cancellation token for its own loop.
                Task clientTask = Task.Run(() => HandleTrackedClientAsync(pipeStream, token), CancellationToken.None);
                clientTasks.TryAdd(clientTask, 0);

                // Remove task from tracking when it completes to prevent unbounded growth.
                _ = clientTask.ContinueWith(_ => clientTasks.TryRemove(clientTask, out byte _), TaskScheduler.Default);
            }
        }
        finally
        {
            _output.WriteLine("CoordinatorServer: Accept loop exiting");
            _heartbeatMonitor?.Dispose();
            _shutdownTimer?.Dispose();

            // Wait for all remaining client tasks to complete before exiting.
            // This ensures logging (which may reference the test context) completes cleanly.
            ICollection<Task> remainingTasks = clientTasks.Keys;

            if (remainingTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(remainingTasks);
                }
                catch
                {
                    // Swallow exceptions from client tasks; they're already logged.
                }
            }
        }
    }

    /// <summary>
    ///  Waits for a client to connect to the named pipe.
    /// </summary>
    /// <param name="token">Cancellation token to abort the wait.</param>
    /// <returns>
    ///  The connected pipe stream, or <see langword="null"/> if the wait was cancelled.
    /// </returns>
    private async Task<NamedPipeServerStream?> WaitForClientAsync(CancellationToken token)
    {
        NamedPipeServerStream pipeStream = new(
            _pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        try
        {
            await pipeStream.WaitForConnectionAsync(token);

            Interlocked.Increment(ref _activeConnections);

            return pipeStream;
        }
        catch (OperationCanceledException)
        {
            pipeStream.Dispose();
            return null;
        }
    }

    /// <summary>
    ///  Tracks a connected client from pipe acceptance through negotiation and its granted lifetime.
    /// </summary>
    /// <param name="pipeStream">The connected pipe stream for this client.</param>
    /// <param name="token">Cancellation token to signal the client loop should exit.</param>
    private async Task HandleTrackedClientAsync(NamedPipeServerStream pipeStream, CancellationToken token)
    {
        try
        {
            await HandleClientAsync(pipeStream, token);
        }
        finally
        {
            Interlocked.Decrement(ref _activeConnections);

            if (!_cts.IsCancellationRequested && IsIdle)
            {
                ResetShutdownTimer();
            }
        }
    }

    /// <summary>
    ///  Handles a single client connection for its entire lifetime.
    /// </summary>
    /// <param name="pipeStream">The connected pipe stream for this client.</param>
    /// <param name="token">Cancellation token to signal the client loop should exit.</param>
    private async Task HandleClientAsync(NamedPipeServerStream pipeStream, CancellationToken token)
    {
        using Connection? connection = Connection.TryCreate(pipeStream, _output);
        if (connection is null)
        {
            return;
        }

        if (!TryProcessGrantRequest(connection, out BuildGrant? grant))
        {
            return;
        }

        if (!TryAcceptClient(connection, grant, out ConnectedClient? client))
        {
            return;
        }

        try
        {
            bool stopProcessing = false;

            // Process subsequent messages (heartbeats and release).
            while (!stopProcessing && !token.IsCancellationRequested && client.IsConnected)
            {
                ClientMessage message;

                try
                {
                    message = await Task.Run(client.ReadClientMessage, token);
                }
                catch (EndOfStreamException)
                {
                    _output.WriteLine($"CoordinatorServer: PID {connection.ProcessId} disconnected (end of stream)");

                    // Client disconnected.
                    break;
                }
                catch (IOException)
                {
                    _output.WriteLine($"CoordinatorServer: PID {connection.ProcessId} disconnected (pipe broken)");

                    // Pipe broken.
                    break;
                }

                switch (message)
                {
                    case HeartbeatMessage:
                        grant.LastHeartbeat = DateTime.UtcNow;
                        break;

                    case ReleaseNodesMessage:
                        _output.WriteLine($"CoordinatorServer: PID {connection.ProcessId} released grant");
                        stopProcessing = true;
                        break;

                    default:
                        _output.WriteLine($"CoordinatorServer: PID {connection.ProcessId} sent unexpected message {message.GetType().Name}");
                        client.WriteServerMessage(new ErrorMessage("Unexpected message after grant"));
                        stopProcessing = true;
                        break;
                }
            }
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            _output.WriteLine($"CoordinatorServer: Exception handling client: {ex.Message}");

            // Swallow exceptions from individual client handling.
        }
        finally
        {
            ReleaseClient(client);
            client.Dispose();
        }
    }

    private bool TryProcessGrantRequest(Connection connection, [NotNullWhen(true)] out BuildGrant? grant)
    {
        try
        {
            ClientMessage clientMessage = connection.ReadClientMessage();

            switch (clientMessage)
            {
                case RequestNodesMessage { RequestedNodes: int requestedNodes }:
                    if (RejectInvalidRequestedNodes(connection, requestedNodes))
                    {
                        grant = null;
                        return false;
                    }

                    grant = new(connection.Id, connection.ProcessId, requestedNodes, isNested: false);
                    return true;

                case JoinGrantMessage { GrantId: Guid grantId, RequestedNodes: int requestedNodes }:
                    _output.WriteLine($"CoordinatorServer: Client requested to join grant {grantId} for {requestedNodes} nodes");

                    if (!connection.ClientCapabilities.Contains(Capabilities.NestedGrants))
                    {
                        _output.WriteLine("CoordinatorServer: Rejected client — JoinGrant requires nested-grants capability");
                        connection.WriteServerMessage(new ErrorMessage("JoinGrant requires nested-grants capability"));

                        grant = null;
                        return false;
                    }

                    if (RejectInvalidRequestedNodes(connection, requestedNodes))
                    {
                        grant = null;
                        return false;
                    }

                    if (!TryCreateNestedGrant(connection, grantId, requestedNodes, out grant))
                    {
                        _output.WriteLine("CoordinatorServer: Rejected nested client — requested grant is not active");
                        connection.WriteServerMessage(new ErrorMessage("Requested coordinator grant is not active"));

                        return false;
                    }

                    return true;

                default:
                    _output.WriteLine($"CoordinatorServer: Rejected client — second message was {clientMessage.GetType().Name}");
                    connection.WriteServerMessage(new ErrorMessage($"Second message must be {nameof(ClientMessageType.RequestNodes)} or {nameof(ClientMessageType.JoinGrant)}"));

                    grant = null;
                    return false;
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"CoordinatorServer: Exception handling client: {ex}");

            grant = null;
            return false;
        }

        bool RejectInvalidRequestedNodes(Connection connection, int requestedNodes)
        {
            if (requestedNodes > 0)
            {
                return false;
            }

            _output.WriteLine($"CoordinatorServer: Rejected client — invalid request (RequestedNodes={requestedNodes})");
            connection.WriteServerMessage(new ErrorMessage("Invalid request: RequestedNodes must be > 0"));
            return true;
        }

        bool TryCreateNestedGrant(Connection connection, Guid grantId, int requestedNodes, [NotNullWhen(true)] out BuildGrant? grant)
        {
            using (_clientsLock.EnterDisposableReadLock())
            {
                if (_activeRootGrantsById.TryGetValue(grantId, out BuildGrant? rootGrant) &&
                    rootGrant.IsActive)
                {
                    grant = new BuildGrant(connection.Id, connection.ProcessId, requestedNodes, grantId, isNested: true)
                    {
                        GrantedNodes = Math.Min(requestedNodes, rootGrant.GrantedNodes),
                    };

                    _output.WriteLine($"CoordinatorServer: Nested client joined grant {grantId} with {grant.GrantedNodes} node(s)");

                    return true;
                }
            }

            grant = null;
            return false;
        }
    }

    private bool TryAcceptClient(Connection connection, BuildGrant grant, [NotNullWhen(true)] out ConnectedClient? client)
    {
        client = null;

        try
        {
            _output.WriteLine($"CoordinatorServer: Client connected (PID {connection.ProcessId}, ConnectionId {connection.Id}, requested {grant.RequestedNodes} nodes)");

            // Once a client is accepted, transfer pipe ownership to ConnectedClient so
            // cleanup and subsequent message I/O are tied to the grant lifecycle.
            client = new ConnectedClient(connection, grant);

            using (_clientsLock.EnterDisposableWriteLock())
            {
                _clientsById[connection.Id] = client;
            }

            int grantedNodes = _budgetManager.TryGrant(grant);

            if (grantedNodes > 0)
            {
                _output.WriteLine($"CoordinatorServer: Granted {grantedNodes} nodes to PID {connection.ProcessId}");

                // Only root grants are tracked by grant ID. Nested grants reference their root
                // grant ID, but releasing a nested connection must not invalidate that root grant.
                if (!grant.IsNested)
                {
                    TrackRootGrant(grant);
                }

                WriteGrantMessage(client, grant);
            }
            else
            {
                _output.WriteLine($"CoordinatorServer: PID {connection.ProcessId} queued (no nodes available)");
                client.WriteServerMessage(WaitMessage.Instance);

                // The grant will be fulfilled later when resources free up.
            }

            ResetShutdownTimer();
            return true;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"CoordinatorServer: Exception accepting client: {ex}");

            if (client is not null)
            {
                ReleaseClient(client);
                client.Dispose();
                client = null;
            }

            return false;
        }
    }

    /// <summary>
    ///  Releases a client's grant and notifies any builds that were waiting for resources.
    /// </summary>
    /// <param name="client">The client whose grant is being released.</param>
    private void ReleaseClient(ConnectedClient client)
    {
        using (_clientsLock.EnterDisposableWriteLock())
        {
            // Only remove if this connection is still current for the connection ID.
            if (_clientsById.TryGetValue(client.ConnectionId, out var current) &&
                current == client)
            {
                _clientsById.Remove(client.ConnectionId);
            }

            if (!client.Grant.IsNested)
            {
                _activeRootGrantsById.Remove(client.Grant.GrantId);
            }
        }

        ImmutableArray<BuildGrant> newlyGranted = _budgetManager.Release(client.Grant);

        if (newlyGranted.Length > 0)
        {
            _output.WriteLine($"CoordinatorServer: Draining wait queue, {newlyGranted.Length} build(s) to notify");
        }

        // Notify newly granted builds outside the locks.
        foreach (BuildGrant grant in newlyGranted)
        {
            bool found;
            ConnectedClient? waitingClient;
            using (_clientsLock.EnterDisposableReadLock())
            {
                found = _clientsById.TryGetValue(grant.ConnectionId, out waitingClient);
            }

            if (found && waitingClient is not null)
            {
                try
                {
                    _output.WriteLine($"CoordinatorServer: Granting {grant.GrantedNodes} deferred nodes to PID {grant.ProcessId}");
                    TrackRootGrant(grant);
                    WriteGrantMessage(waitingClient, grant);
                }
                catch (IOException)
                {
                    _output.WriteLine($"CoordinatorServer: PID {grant.ProcessId} disconnected while waiting");

                    // Client disconnected while waiting. Release their grant too.
                    ReleaseClient(waitingClient);
                }
            }
        }

        ResetShutdownTimer();
    }

    private void TrackRootGrant(BuildGrant grant)
    {
        using (_clientsLock.EnterDisposableWriteLock())
        {
            _activeRootGrantsById[grant.GrantId] = grant;
        }
    }

    private static void WriteGrantMessage(ConnectedClient client, BuildGrant grant)
    {
        if (client.Capabilities.Contains(Capabilities.NestedGrants))
        {
            client.WriteServerMessage(new NodeGrantMessage(grant.GrantId, grant.GrantedNodes));
        }
        else
        {
            client.WriteServerMessage(new NodeGrantMessage(grant.GrantedNodes));
        }
    }

    /// <summary>
    ///  Periodically checks for builds that have missed heartbeats and reclaims their grants.
    /// </summary>
    /// <param name="state">Timer callback state (unused).</param>
    private void CheckHeartbeats(object? state)
    {
        DateTime threshold = DateTime.UtcNow - TimeSpan.FromMilliseconds(_settings.HeartbeatTimeoutMs);

        List<ConnectedClient> clientsToCheck;

        using (_clientsLock.EnterDisposableReadLock())
        {
            clientsToCheck = [.. _clientsById.Values];
        }

        foreach (ConnectedClient client in clientsToCheck)
        {
            if (client.Grant.LastHeartbeat >= threshold)
            {
                continue;
            }

            // Check if the process is still alive before reclaiming.
            if (IsProcessAlive(client.ProcessId))
            {
                continue;
            }

            _output.WriteLine($"CoordinatorServer: Reclaiming grant from dead PID {client.ProcessId}");

            // ReleaseClient will acquire its own write lock.
            ReleaseClient(client);
        }
    }

    /// <summary>
    ///  Checks whether a process with the given ID is still running.
    /// </summary>
    /// <param name="processId">The OS process ID to check.</param>
    /// <returns>
    ///  <see langword="true"/> if the process exists and has not exited; otherwise <see langword="false"/>.
    /// </returns>
    private static bool IsProcessAlive(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // Process doesn't exist.
            return false;
        }
    }

    /// <summary>
    ///  Resets the auto-shutdown timer. If no builds are active or waiting when the timer
    ///  fires, the coordinator shuts down.
    /// </summary>
    private void ResetShutdownTimer()
    {
        var newTimer = new Timer(
            _ =>
            {
                if (IsIdle)
                {
                    _output.WriteLine("CoordinatorServer: Auto-shutdown (no active or waiting builds)");
                    _cts.Cancel();
                }
            },
            state: null,
            dueTime: _shutdownTimeoutMs,
            period: Timeout.Infinite);

        Interlocked.Exchange(ref _shutdownTimer, newTimer)?.Dispose();
    }
}
