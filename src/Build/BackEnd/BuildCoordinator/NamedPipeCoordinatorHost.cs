// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Named pipe transport layer for the BuildCoordinator.
    /// Listens on a well-known pipe, translates the text protocol into
    /// coordinator method calls, and notifies promoted builds via wait pipes.
    ///
    /// Protocol (line-based text over named pipe):
    ///   REGISTER buildId requestedNodes → OK grantedNodes | QUEUED position total
    ///   HEARTBEAT buildId              → OK
    ///   UNREGISTER buildId             → OK [promoted N]
    ///   STATUS                         → OK budget=N active=N queued=N max=N
    ///   SHUTDOWN                       → OK
    /// </summary>
    public sealed class NamedPipeCoordinatorHost : IDisposable
    {
        private readonly BuildCoordinator _coordinator;
        private readonly string? _pipeNameOverride;
        private readonly CancellationTokenSource _cts = new();
        private Task? _listenTask;

        /// <summary>
        /// Well-known pipe name scoped to the current user.
        /// On Unix: /tmp/MSBuild-Coordinator-{username}
        /// On Windows: MSBuild-Coordinator-{username}
        /// </summary>
        internal static string GetPipeName()
        {
            string user = Environment.UserName;
            string pipeName = $"MSBuild-Coordinator-{user}";
            return NativeMethodsShared.IsUnixLike ? $"/tmp/{pipeName}" : pipeName;
        }

        public NamedPipeCoordinatorHost(INodeBudgetPolicy policy, int startupDelayMs = 0, string? pipeName = null)
        {
            _pipeNameOverride = pipeName;
            _coordinator = new BuildCoordinator(policy, startupDelayMs, NotifyPromotedBuild);
        }

        /// <summary>
        /// Returns the well-known wait pipe name for a given coordinator pipe and build ID.
        /// Both client and host derive this from the same naming convention.
        /// </summary>
        internal static string GetWaitPipeName(string coordinatorPipe, string buildId) => $"{coordinatorPipe}-{buildId}";

        internal BuildCoordinator Coordinator => _coordinator;

        internal string EffectivePipeName => _pipeNameOverride ?? GetPipeName();

        public void Start()
        {
            string pipeName = EffectivePipeName;

            if (NativeMethodsShared.IsUnixLike && File.Exists(pipeName))
            {
                if (TryConnectToExisting(pipeName))
                {
                    Console.WriteLine($"WARNING: Another coordinator may be running on {pipeName}. Taking over...");
                }

                File.Delete(pipeName);
            }

            var status = _coordinator.GetStatus();
            Console.WriteLine($"Build Coordinator starting");
            Console.WriteLine($"  Pipe: {pipeName}");
            Console.WriteLine($"  Budget: {status.TotalBudget} nodes");
            Console.WriteLine($"  Max concurrent builds: {status.MaxConcurrentBuilds}");

            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            _coordinator.StartReaper();
        }

        public void Stop()
        {
            _cts.Cancel();
            _listenTask?.Wait(TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            Stop();
            _coordinator.Dispose();
            _cts.Dispose();
        }

        public void WaitForShutdown()
        {
            try
            {
                _listenTask?.Wait();
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
            }
        }

        private void ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                string pipeName = EffectivePipeName;

#pragma warning disable CA2000 // Dispose is called in the Task.Run finally block
                var server = new NamedPipeServerStream(
                    pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    System.IO.Pipes.PipeOptions.CurrentUserOnly);
#pragma warning restore CA2000

                try
                {
                    server.WaitForConnectionAsync(ct).Wait(ct);
                }
                catch (OperationCanceledException)
                {
                    server.Dispose();
                    break;
                }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
                {
                    server.Dispose();
                    break;
                }

                var capturedServer = server;
                Task.Run(() =>
                {
                    try
                    {
                        HandleConnection(capturedServer);
                    }
                    finally
                    {
                        capturedServer.Dispose();
                    }
                }, ct);
            }
        }

        private void HandleConnection(NamedPipeServerStream server)
        {
            try
            {
                using var reader = new StreamReader(server, leaveOpen: true);
                using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };

                string? line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                {
                    return;
                }

                string[] parts = line.Split(' ');
                string command = parts[0].ToUpperInvariant();

                switch (command)
                {
                    case "REGISTER":
                        HandleRegister(parts, writer);
                        break;
                    case "HEARTBEAT":
                        HandleHeartbeat(parts, writer);
                        break;
                    case "UNREGISTER":
                        HandleUnregister(parts, writer);
                        break;
                    case "STATUS":
                        HandleStatus(writer);
                        break;
                    case "SHUTDOWN":
                        writer.WriteLine("OK");
                        _cts.Cancel();
                        break;
                    default:
                        writer.WriteLine("ERR unknown command");
                        break;
                }
            }
            catch (IOException)
            {
                // Client disconnected
            }
        }

        private void HandleRegister(string[] parts, StreamWriter writer)
        {
            if (parts.Length < 3)
            {
                writer.WriteLine("ERR usage: REGISTER buildId requestedNodes");
                return;
            }

            if (!int.TryParse(parts[2], out int requested) || requested <= 0)
            {
                writer.WriteLine("ERR invalid requestedNodes");
                return;
            }

            var result = _coordinator.Register(parts[1], requested);

            if (result.Outcome == RegisterOutcome.Granted)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] REGISTER {parts[1]}: requested={requested} granted={result.GrantedNodes}");
                writer.WriteLine($"OK {result.GrantedNodes}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] QUEUED {parts[1]}: position={result.QueuePosition}");
                writer.WriteLine($"QUEUED {result.QueuePosition} {result.QueueTotal}");
            }
        }

        private void HandleHeartbeat(string[] parts, StreamWriter writer)
        {
            if (parts.Length < 2)
            {
                writer.WriteLine("ERR usage: HEARTBEAT buildId");
                return;
            }

            _coordinator.Heartbeat(parts[1]);
            writer.WriteLine("OK");
        }

        private void HandleUnregister(string[] parts, StreamWriter writer)
        {
            if (parts.Length < 2)
            {
                writer.WriteLine("ERR usage: UNREGISTER buildId");
                return;
            }

            int promoted = _coordinator.Unregister(parts[1]);
            var status = _coordinator.GetStatus();
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] UNREGISTER {parts[1]}: active={status.ActiveCount} queued={status.QueuedCount}");

            if (promoted > 0)
            {
                writer.WriteLine($"OK promoted {promoted}");
            }
            else
            {
                writer.WriteLine("OK");
            }
        }

        private void HandleStatus(StreamWriter writer)
        {
            var status = _coordinator.GetStatus();
            writer.WriteLine($"OK budget={status.TotalBudget} active={status.ActiveCount} queued={status.QueuedCount} max={status.MaxConcurrentBuilds}");
        }

        private void NotifyPromotedBuild(string buildId, int granted)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] PROMOTED {buildId}: granted={granted}");

            string waitPipeName = GetWaitPipeName(EffectivePipeName, buildId);
            try
            {
                using var client = new NamedPipeClientStream(".", waitPipeName, PipeDirection.Out, System.IO.Pipes.PipeOptions.CurrentUserOnly);
                client.Connect(5000);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine($"OK {granted}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WARNING: Failed to notify {buildId} via wait pipe: {ex.Message}");
            }
            catch (TimeoutException ex)
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WARNING: Failed to notify {buildId} via wait pipe: {ex.Message}");
            }
        }

        private static bool TryConnectToExisting(string pipeName)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, System.IO.Pipes.PipeOptions.CurrentUserOnly);
                client.Connect(500);
                using var writer = new StreamWriter(client, leaveOpen: true) { AutoFlush = true };
                using var reader = new StreamReader(client, leaveOpen: true);
                writer.WriteLine("STATUS");
                string? response = reader.ReadLine();
                return response != null && response.StartsWith("OK", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }
    }
}
