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
    /// When the coordinator is at capacity, TryRegister will block on a per-build wait pipe
    /// until the coordinator promotes it to active.
    /// </summary>
    internal sealed class NamedPipeCoordinatorClient : ICoordinatorClient
    {
        private readonly string _buildId;
        private readonly string _pipeName;
        private int _grantedNodes;
        private Timer? _heartbeatTimer;
        private bool _registered;

        public bool IsConnected => _registered;
        public int GrantedNodes => _grantedNodes;
        internal string BuildId => _buildId;

        internal NamedPipeCoordinatorClient()
            : this(NamedPipeCoordinatorHost.GetPipeName())
        {
        }

        /// <summary>
        /// Internal constructor for tests — allows connecting to a coordinator on a custom pipe name.
        /// </summary>
        internal NamedPipeCoordinatorClient(string pipeName)
        {
            _buildId = $"{Environment.ProcessId}-{DateTime.UtcNow.Ticks}";
            _pipeName = pipeName;
        }



        /// <summary>
        /// Try to register with the coordinator. Returns true if a coordinator was found
        /// and the build was registered (or promoted from queue).
        ///
        /// If the coordinator has capacity, returns immediately with grantedNodes.
        /// If queued, blocks on a per-build wait pipe until the coordinator promotes it.
        /// </summary>
        public bool TryRegister(int requestedNodes, out int grantedNodes, CancellationToken ct = default)
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

            // Queued — block on wait pipe until promoted
            if (response.StartsWith("QUEUED ", StringComparison.Ordinal))
            {
                return WaitForPromotion(out grantedNodes, ct);
            }

            return false;
        }

        /// <summary>
        /// Create a per-build wait pipe and block until the coordinator writes the promotion
        /// message. The pipe name is derived by convention from the coordinator pipe + buildId,
        /// so neither side needs to communicate it.
        /// </summary>
        private bool WaitForPromotion(out int grantedNodes, CancellationToken ct)
        {
            grantedNodes = 0;
            string waitPipeName = NamedPipeCoordinatorHost.GetWaitPipeName(_pipeName, _buildId);

            try
            {
                using var waitPipe = new NamedPipeServerStream(
                    waitPipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    System.IO.Pipes.PipeOptions.CurrentUserOnly);

                waitPipe.WaitForConnectionAsync(ct).Wait(ct);

                using var reader = new StreamReader(waitPipe);
                string? line = reader.ReadLine();

                if (line != null && line.StartsWith("OK ", StringComparison.Ordinal))
                {
                    if (int.TryParse(line.AsSpan(3), out int granted) && granted > 0)
                    {
                        _grantedNodes = granted;
                        grantedNodes = granted;
                        _registered = true;
                        return true;
                    }
                }

                return false;
            }
            catch (OperationCanceledException)
            {
                // Build was cancelled while queued — unregister
                SendCommand($"UNREGISTER {_buildId}");
                return false;
            }
            catch (Exception)
            {
                // Pipe error — fall through uncoordinated
                return false;
            }
            finally
            {
                // Clean up pipe file on Unix
                if (!OperatingSystem.IsWindows())
                {
                    try { File.Delete(waitPipeName); } catch { }
                }
            }
        }

        /// <summary>
        /// Start periodic heartbeats so the coordinator can detect stale builds.
        /// Budget is fixed at registration — heartbeat is purely a liveness signal.
        /// </summary>
        public void StartHeartbeat()
        {
            _heartbeatTimer = new Timer(HeartbeatCallback, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        /// <summary>
        /// Unregister from the coordinator and stop heartbeats.
        /// </summary>
        public void Unregister()
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

            // Heartbeat is purely a liveness signal — budget is fixed at registration.
            SendCommand($"HEARTBEAT {_buildId}");
        }

        /// <summary>
        /// Send a command to the coordinator and return the response line, or null if connection fails.
        /// Retries up to 5 times with 500ms delay to handle the brief gap between listener re-binds
        /// when many builds connect simultaneously.
        /// </summary>
        private string? SendCommand(string command)
        {
            const int maxAttempts = 5;
            const int connectTimeoutMs = 5000;
            const int retryDelayMs = 500;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, System.IO.Pipes.PipeOptions.CurrentUserOnly);
                    client.Connect(connectTimeoutMs);

                    using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
                    using var reader = new StreamReader(client, leaveOpen: true);

                    writer.WriteLine(command);
                    return reader.ReadLine();
                }
                catch (TimeoutException)
                {
                    if (attempt < maxAttempts - 1)
                    {
                        Thread.Sleep(retryDelayMs);
                    }
                }
                catch (IOException)
                {
                    if (attempt < maxAttempts - 1)
                    {
                        Thread.Sleep(retryDelayMs);
                    }
                }
            }

            return null;
        }
    }
}
