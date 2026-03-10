// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Client used by BuildManager to communicate with an external BuildCoordinator process.
    /// If no coordinator is running, all operations gracefully no-op and the build runs with
    /// its original MaxNodeCount.
    ///
    /// When the coordinator is at capacity, TryRegister will block (heartbeating for position
    /// updates) until the build is promoted to active.
    /// </summary>
    internal sealed class BuildCoordinatorClient : IDisposable
    {
        private readonly string _buildId;
        private readonly string _pipeName;
        private int _grantedNodes;
        private Timer? _heartbeatTimer;
        private bool _registered;

        internal bool IsConnected => _registered;
        internal int GrantedNodes => _grantedNodes;
        internal string BuildId => _buildId;

        internal BuildCoordinatorClient()
            : this(BuildCoordinator.GetPipeName())
        {
        }

        /// <summary>
        /// Internal constructor for tests — allows connecting to a coordinator on a custom pipe name.
        /// </summary>
        internal BuildCoordinatorClient(string pipeName)
        {
            _buildId = $"{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
            _pipeName = pipeName;
        }

        /// <summary>
        /// Try to register with the coordinator. Returns true if a coordinator was found
        /// and the build was registered (or promoted from queue).
        ///
        /// If the coordinator has capacity, returns immediately with grantedNodes.
        /// If queued, blocks and heartbeats until promoted, calling onQueuePositionChanged
        /// with (position, totalQueued, waitSeconds) on each update.
        /// </summary>
        internal bool TryRegister(int requestedNodes, out int grantedNodes, Action<int, int, int>? onQueuePositionChanged = null, CancellationToken ct = default)
        {
            grantedNodes = requestedNodes;

            string? response = SendCommand($"REGISTER {_buildId} {requestedNodes}");
            if (response == null)
            {
                return false;
            }

            // Immediate grant
            if (response.StartsWith("OK ", StringComparison.Ordinal))
            {
                if (int.TryParse(response.AsSpan(3), out int granted) && granted > 0)
                {
                    _grantedNodes = granted;
                    grantedNodes = granted;
                    _registered = true;
                    return true;
                }

                return false;
            }

            // Queued — block and heartbeat until promoted
            if (response.StartsWith("QUEUED ", StringComparison.Ordinal))
            {
                return WaitInQueue(requestedNodes, out grantedNodes, onQueuePositionChanged, ct);
            }

            return false;
        }

        /// <summary>
        /// Block heartbeating until the coordinator promotes this build to active.
        /// </summary>
        private bool WaitInQueue(int requestedNodes, out int grantedNodes, Action<int, int, int>? onQueuePositionChanged, CancellationToken ct)
        {
            grantedNodes = requestedNodes;

            while (!ct.IsCancellationRequested)
            {
                Thread.Sleep(2000); // Heartbeat interval

                string? hbResponse = SendCommand($"HEARTBEAT {_buildId}");
                if (hbResponse == null)
                {
                    // Coordinator gone — fall through with original node count
                    return false;
                }

                // Promoted to active!
                if (hbResponse.StartsWith("OK ", StringComparison.Ordinal))
                {
                    if (int.TryParse(hbResponse.AsSpan(3), out int granted) && granted > 0)
                    {
                        _grantedNodes = granted;
                        grantedNodes = granted;
                        _registered = true;
                        return true;
                    }

                    return false;
                }

                // Still queued — parse position info: "QUEUED position totalQueued waitSec"
                if (hbResponse.StartsWith("QUEUED ", StringComparison.Ordinal))
                {
                    string[] parts = hbResponse.Split(' ');
                    if (parts.Length >= 4
                        && int.TryParse(parts[1], out int position)
                        && int.TryParse(parts[2], out int totalQueued)
                        && int.TryParse(parts[3], out int waitSec))
                    {
                        onQueuePositionChanged?.Invoke(position, totalQueued, waitSec);
                    }
                }
            }

            // Cancelled — unregister
            SendCommand($"UNREGISTER {_buildId}");
            return false;
        }

        /// <summary>
        /// Start periodic heartbeats for liveness so the coordinator can detect stale builds.
        /// The heartbeat response includes the current budget, which is stored in GrantedNodes.
        /// The coordinator uses heartbeat acknowledgment to gate promotion of queued builds,
        /// but BuildManager does not change MaxNodeCount mid-build — the acknowledged budget
        /// only matters for the coordinator's internal bookkeeping.
        /// </summary>
        internal void StartHeartbeat()
        {
            _heartbeatTimer = new Timer(HeartbeatCallback, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        /// <summary>
        /// Unregister from the coordinator and stop heartbeats.
        /// </summary>
        internal void Unregister()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;

            if (_registered)
            {
                SendCommand($"UNREGISTER {_buildId}");
                _registered = false;
            }
        }

        public void Dispose()
        {
            Unregister();
        }

        private void HeartbeatCallback(object? state)
        {
            if (!_registered)
            {
                return;
            }

            string? response = SendCommand($"HEARTBEAT {_buildId}");
            if (response != null && response.StartsWith("OK ", StringComparison.Ordinal))
            {
                if (int.TryParse(response.AsSpan(3), out int newBudget) && newBudget > 0)
                {
                    _grantedNodes = newBudget;
                }
            }
        }

        /// <summary>
        /// Send a command to the coordinator and return the response line, or null if connection fails.
        /// Retries up to 3 times with 300ms delay to handle the brief gap between listener re-binds.
        /// </summary>
        private string? SendCommand(string command)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, System.IO.Pipes.PipeOptions.CurrentUserOnly);
                    client.Connect(1000);

                    using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
                    using var reader = new StreamReader(client, leaveOpen: true);

                    writer.WriteLine(command);
                    return reader.ReadLine();
                }
                catch (TimeoutException)
                {
                    if (attempt < 2)
                    {
                        Thread.Sleep(300);
                    }
                }
                catch (IOException)
                {
                    if (attempt < 2)
                    {
                        Thread.Sleep(300);
                    }
                }
            }

            return null;
        }
    }
}
