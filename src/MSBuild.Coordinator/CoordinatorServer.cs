// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Build.Framework.Coordinator;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  The coordinator server that listens for MSBuild client connections on a named pipe,
///  manages node grants, and monitors build health via heartbeats.
/// </summary>
internal sealed partial class CoordinatorServer(
    CoordinatorSettings settings,
    ICoordinatorLogger? logger = null) : IDisposable
{
    private readonly CoordinatorSettings _settings = settings;
    private readonly NodeBudgetManager _budgetManager = new(settings.TotalNodeBudget);
    private readonly string _pipeName = settings.PipeName;
    private readonly int _heartbeatIntervalMs = settings.HeartbeatIntervalMs;
    private readonly int _missedHeartbeatsThreshold = settings.MissedHeartbeatsThreshold;
    private readonly int _shutdownTimeoutMs = settings.ShutdownTimeoutMs;
    private readonly Dictionary<int, ClientConnection> _connectionsByProcessId = [];
    private readonly object _budgetLock = new();
    private readonly ReaderWriterLockSlim _connectionLock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ICoordinatorLogger _logger = logger ?? DefaultLogger.Instance;
    private Timer? _heartbeatMonitor;
    private Timer? _shutdownTimer;

    /// <summary>
    ///  Runs the coordinator server until cancellation is requested or the auto-shutdown
    ///  timeout elapses with no active or waiting builds.
    /// </summary>
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

        _logger.WriteLine($"Server: Accept loop started on pipe '{_pipeName}' (budget={_settings.TotalNodeBudget})");

        HashSet<Task> clientTasks = [];

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
                Task clientTask = Task.Run(() => HandleClientAsync(pipeStream, token), CancellationToken.None);
                clientTasks.Add(clientTask);

                // Remove task from tracking when it completes to prevent unbounded growth.
                _ = clientTask.ContinueWith(_ => clientTasks.Remove(clientTask), TaskScheduler.Default);
            }
        }
        finally
        {
            _logger.WriteLine("Server: Accept loop exiting");
            _heartbeatMonitor?.Dispose();
            _shutdownTimer?.Dispose();

            // Wait for all remaining client tasks to complete before exiting.
            // This ensures logging (which may reference the test context) completes cleanly.
            if (clientTasks.Count > 0)
            {
                try
                {
                    await Task.WhenAll(clientTasks);
                }
                catch
                {
                    // Swallow exceptions from client tasks; they're already logged.
                }
            }
        }
    }

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
            return pipeStream;
        }
        catch (OperationCanceledException)
        {
            pipeStream.Dispose();
            return null;
        }
    }

    /// <summary>
    ///  Handles a single client connection for its entire lifetime.
    /// </summary>
    private async Task HandleClientAsync(NamedPipeServerStream pipeStream, CancellationToken token)
    {
        ClientConnection? connection = null;

        try
        {
            using BinaryReader initialReader = new(pipeStream, System.Text.Encoding.UTF8, leaveOpen: true);

            // The first message must be RequestNodes.
            ClientMessage firstMessage = initialReader.ReadClientMessage();

            if (firstMessage is not RequestNodesMessage request)
            {
                _logger.WriteLine($"Server: Rejected client — first message was {firstMessage.GetType().Name}");
                using BinaryWriter errorWriter = new(pipeStream, System.Text.Encoding.UTF8, leaveOpen: true);
                errorWriter.Write(new ErrorMessage("First message must be RequestNodes"));
                pipeStream.Dispose();
                return;
            }

            _logger.WriteLine($"Server: Client connected (PID {request.ProcessId}, requested {request.RequestedNodes} nodes)");

            BuildGrant grant = new(request.ProcessId, request.RequestedNodes);
            connection = new ClientConnection(grant, pipeStream);

            using (_connectionLock.EnterDisposableWriteLock())
            {
                _connectionsByProcessId[request.ProcessId] = connection;
            }

            // Try to grant nodes.
            int grantedNodes;

            lock (_budgetLock)
            {
                grantedNodes = _budgetManager.TryGrant(grant);
            }

            if (grantedNodes > 0)
            {
                _logger.WriteLine($"Server: Granted {grantedNodes} nodes to PID {request.ProcessId}");
                connection.Writer.Write(new NodeGrantMessage(grantedNodes));
            }
            else
            {
                _logger.WriteLine($"Server: PID {request.ProcessId} queued (no nodes available)");
                connection.Writer.Write(WaitMessage.Instance);

                // The grant will be fulfilled later when resources free up.
            }

            ResetShutdownTimer();

            // Process subsequent messages (heartbeats and release).
            while (!token.IsCancellationRequested && pipeStream.IsConnected)
            {
                ClientMessage message;

                try
                {
                    message = await Task.Run(() => connection.Reader.ReadClientMessage(), token);
                }
                catch (EndOfStreamException)
                {
                    _logger.WriteLine($"Server: PID {request.ProcessId} disconnected (end of stream)");

                    // Client disconnected.
                    break;
                }
                catch (IOException)
                {
                    _logger.WriteLine($"Server: PID {request.ProcessId} disconnected (pipe broken)");

                    // Pipe broken.
                    break;
                }

                switch (message)
                {
                    case HeartbeatMessage:
                        grant.LastHeartbeat = DateTime.UtcNow;
                        break;

                    case ReleaseNodesMessage:
                        _logger.WriteLine($"Server: PID {request.ProcessId} released grant");
                        ReleaseConnection(connection);
                        return;
                }
            }
        }
        catch (Exception ex) when (!Debugger.IsAttached)
        {
            _logger.WriteLine($"Server: Exception handling client: {ex.Message}");

            // Swallow exceptions from individual client handling.
        }
        finally
        {
            // If we get here without an explicit release, treat it as a crash/disconnect.
            if (connection is not null)
            {
                ReleaseConnection(connection);
                connection.Dispose();
            }
            else
            {
                pipeStream.Dispose();
            }
        }
    }

    /// <summary>
    ///  Releases a connection's grant and notifies any builds that were waiting for resources.
    /// </summary>
    private void ReleaseConnection(ClientConnection connection)
    {
        using (_connectionLock.EnterDisposableWriteLock())
        {
            // Only remove if this connection is still current for the PID.
            if (_connectionsByProcessId.TryGetValue(connection.Grant.ProcessId, out var current) &&
                current == connection)
            {
                _connectionsByProcessId.Remove(connection.Grant.ProcessId);
            }
        }

        ImmutableArray<BuildGrant> newlyGranted;

        lock (_budgetLock)
        {
            newlyGranted = _budgetManager.Release(connection.Grant);
        }

        if (newlyGranted.Length > 0)
        {
            _logger.WriteLine($"Server: Draining wait queue, {newlyGranted.Length} build(s) to notify");
        }

        // Notify newly granted builds outside the locks.
        foreach (BuildGrant grant in newlyGranted)
        {
            bool found;
            ClientConnection? waitingConnection;
            using (_connectionLock.EnterDisposableReadLock())
            {
                found = _connectionsByProcessId.TryGetValue(grant.ProcessId, out waitingConnection);
            }

            if (found && waitingConnection is not null)
            {
                try
                {
                    _logger.WriteLine($"Server: Granting {grant.GrantedNodes} deferred nodes to PID {grant.ProcessId}");
                    waitingConnection.Writer.Write(new NodeGrantMessage(grant.GrantedNodes));
                }
                catch (IOException)
                {
                    _logger.WriteLine($"Server: PID {grant.ProcessId} disconnected while waiting");

                    // Client disconnected while waiting. Release their grant too.
                    ReleaseConnection(waitingConnection);
                }
            }
        }

        ResetShutdownTimer();
    }

    /// <summary>
    ///  Periodically checks for builds that have missed heartbeats and reclaims their grants.
    /// </summary>
    private void CheckHeartbeats(object? state)
    {
        DateTime threshold = DateTime.UtcNow - TimeSpan.FromMilliseconds(_heartbeatIntervalMs * _missedHeartbeatsThreshold);

        List<ClientConnection> connectionsToCheck;

        using (_connectionLock.EnterDisposableReadLock())
        {
            connectionsToCheck = [.. _connectionsByProcessId.Values];
        }

        foreach (ClientConnection connection in connectionsToCheck)
        {
            if (connection.Grant.LastHeartbeat >= threshold)
            {
                continue;
            }

            // Check if the process is still alive before reclaiming.
            if (IsProcessAlive(connection.Grant.ProcessId))
            {
                continue;
            }

            _logger.WriteLine($"Server: Reclaiming grant from dead PID {connection.Grant.ProcessId}");

            // ReleaseConnection will acquire its own write lock.
            ReleaseConnection(connection);
        }
    }

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
        _shutdownTimer?.Dispose();
        _shutdownTimer = new Timer(
            _ =>
            {
                lock (_budgetLock)
                {
                    if (_budgetManager.ActiveBuildCount == 0 && _budgetManager.WaitingBuildCount == 0)
                    {
                        _logger.WriteLine("Server: Auto-shutdown (no active or waiting builds)");
                        _cts.Cancel();
                    }
                }
            },
            state: null,
            dueTime: _shutdownTimeoutMs,
            period: Timeout.Infinite);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _heartbeatMonitor?.Dispose();
        _shutdownTimer?.Dispose();
        _cts.Dispose();
        _connectionLock.Dispose();
    }
}
