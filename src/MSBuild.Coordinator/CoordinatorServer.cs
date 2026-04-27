// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO.Pipes;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  The coordinator server that listens for MSBuild client connections on a named pipe,
///  manages node grants, and monitors build health via heartbeats.
/// </summary>
internal sealed partial class CoordinatorServer(
    int totalBudget,
    string pipeName,
    int heartbeatIntervalMs = Protocol.DefaultHeartbeatIntervalMs,
    int missedHeartbeatsThreshold = Protocol.DefaultMissedHeartbeatsThreshold,
    int shutdownTimeoutMs = 60_000,
    ICoordinatorLogger? logger = null) : IDisposable
{
    private readonly NodeBudgetManager _budgetManager = new(totalBudget);
    private readonly string _pipeName = pipeName;
    private readonly int _heartbeatIntervalMs = heartbeatIntervalMs;
    private readonly int _missedHeartbeatsThreshold = missedHeartbeatsThreshold;
    private readonly int _shutdownTimeoutMs = shutdownTimeoutMs;
    private readonly ConcurrentDictionary<int, ClientConnection> _connectionsByProcessId = new();
    private readonly object _budgetLock = new();
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

        _logger.WriteLine($"Server: Accept loop started on pipe '{_pipeName}' (budget={totalBudget})");

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
                _ = Task.Run(() => HandleClientAsync(pipeStream, token), CancellationToken.None);
            }
        }
        finally
        {
            _logger.WriteLine("Server: Accept loop exiting");
            _heartbeatMonitor?.Dispose();
            _shutdownTimer?.Dispose();
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
            _connectionsByProcessId[request.ProcessId] = connection;

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
        _connectionsByProcessId.TryRemove(connection.Grant.ProcessId, out _);

        ImmutableArray<BuildGrant> newlyGranted;

        lock (_budgetLock)
        {
            newlyGranted = _budgetManager.Release(connection.Grant);
        }

        if (newlyGranted.Length > 0)
        {
            _logger.WriteLine($"Server: Draining wait queue, {newlyGranted.Length} build(s) to notify");
        }

        // Notify newly granted builds outside the lock.
        foreach (BuildGrant grant in newlyGranted)
        {
            if (_connectionsByProcessId.TryGetValue(grant.ProcessId, out ClientConnection? waitingConnection))
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

        foreach (ClientConnection connection in _connectionsByProcessId.Values)
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
    }
}
