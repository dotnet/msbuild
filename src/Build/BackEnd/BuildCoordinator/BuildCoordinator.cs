// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// A standalone coordinator process that manages node budgets across concurrent MSBuild instances.
    ///
    /// Simple capacity model: builds register and are activated immediately if there's capacity
    /// (active count &lt; maxConcurrentBuilds). Otherwise they're queued FIFO and promoted when
    /// a slot opens (via UNREGISTER or staleness reaping). Each active build is granted a fair
    /// share of the total node budget (totalBudget / activeCount), capped at what it requested.
    ///
    /// Protocol (line-based text over named pipe):
    ///   REGISTER buildId requestedNodes
    ///     → OK grantedNodes                    (activated — has capacity)
    ///     → QUEUED position totalQueued         (at capacity — wait for slot)
    ///
    ///   HEARTBEAT buildId
    ///     → OK grantedNodes                    (active build — current budget)
    ///     → QUEUED position totalQueued waitSec (queued build — position update)
    ///
    ///   UNREGISTER buildId
    ///     → OK [promoted buildId]              (promotes next queued build if any)
    ///
    ///   STATUS
    ///     → Multi-line summary of active + queued builds
    ///
    ///   SHUTDOWN
    ///     → OK
    /// </summary>
    public sealed class BuildCoordinator : IDisposable
    {
        /// <summary>
        /// Well-known pipe name scoped to the current user.
        /// On Unix: /tmp/MSBuild-Coordinator-{username}
        /// On Windows: MSBuild-Coordinator-{username}
        /// </summary>
        internal static string GetPipeName()
        {
            string user = Environment.UserName;
            string pipeName = $"MSBuild-Coordinator-{user}";

            if (NativeMethodsShared.IsUnixLike)
            {
                return $"/tmp/{pipeName}";
            }

            return pipeName;
        }

        private readonly int _totalBudget;
        private readonly int _maxConcurrentBuilds;
        private readonly int _minBuildsForBudget;
        private readonly string? _pipeNameOverride;
        private readonly ConcurrentDictionary<string, BuildRegistration> _activeBuilds = new();
        private readonly List<BuildRegistration> _queuedBuilds = new();
        private readonly object _queueLock = new();
        private readonly CancellationTokenSource _cts = new();
        private Task? _listenTask;
        private Timer? _stalenessReaper;
        private Timer? _pipeWatchdog;
        private CancellationTokenSource? _listenCycleCts;

        /// <summary>
        /// If a build hasn't heartbeated in this many seconds, consider it dead.
        /// </summary>
        internal int StaleHeartbeatSeconds { get; set; } = 10;

        /// <summary>
        /// How often the staleness reaper runs (in seconds).
        /// </summary>
        internal int ReaperIntervalSeconds { get; set; } = 5;

        public BuildCoordinator(int totalBudget, int maxConcurrentBuilds, int minBuildsForBudget = 1)
            : this(totalBudget, maxConcurrentBuilds, minBuildsForBudget, pipeName: null)
        {
        }

        /// <summary>
        /// Internal constructor for tests — allows overriding the pipe name to avoid
        /// collisions with a real coordinator or other test instances.
        /// </summary>
        internal BuildCoordinator(int totalBudget, int maxConcurrentBuilds, int minBuildsForBudget, string? pipeName)
        {
            if (totalBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalBudget), totalBudget, "Budget must be at least 1.");
            }

            if (maxConcurrentBuilds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxConcurrentBuilds), maxConcurrentBuilds, "Max concurrent builds must be at least 1.");
            }

            _totalBudget = totalBudget;
            _maxConcurrentBuilds = maxConcurrentBuilds;
            _minBuildsForBudget = Math.Max(1, minBuildsForBudget);
            _pipeNameOverride = pipeName;
        }

        /// <summary>
        /// Returns the effective pipe name, preferring the override if set.
        /// </summary>
        internal string EffectivePipeName => _pipeNameOverride ?? GetPipeName();

        /// <summary>
        /// Start listening for MSBuild client connections.
        /// </summary>
        public void Start()
        {
            string pipeName = EffectivePipeName;

            // On Unix, clean up stale pipe file — warn if another coordinator is alive.
            if (NativeMethodsShared.IsUnixLike && File.Exists(pipeName))
            {
                if (TryConnectToExisting(pipeName))
                {
                    Console.WriteLine($"WARNING: Another coordinator may be running on {pipeName}. Taking over...");
                }

                File.Delete(pipeName);
            }

            Console.WriteLine($"Build Coordinator starting");
            Console.WriteLine($"  Pipe: {pipeName}");
            Console.WriteLine($"  Budget: {_totalBudget} nodes");
            Console.WriteLine($"  Max concurrent builds: {_maxConcurrentBuilds}");
            Console.WriteLine($"  Min builds for budget: {_minBuildsForBudget}");

            _listenTask = Task.Run(() => ListenLoop(_cts.Token));

            // Periodically reap builds that stopped heartbeating (crashed/killed process)
            _stalenessReaper = new Timer(ReapStaleBuilds, null, TimeSpan.FromSeconds(ReaperIntervalSeconds), TimeSpan.FromSeconds(ReaperIntervalSeconds));

            // Watch for pipe file deletion (e.g. by overzealous cleanup scripts)
            if (NativeMethodsShared.IsUnixLike)
            {
                _pipeWatchdog = new Timer(CheckPipeHealth, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
            }
        }

        /// <summary>
        /// Stop the coordinator and clean up.
        /// </summary>
        public void Stop()
        {
            _pipeWatchdog?.Dispose();
            _pipeWatchdog = null;
            _stalenessReaper?.Dispose();
            _stalenessReaper = null;
            _cts.Cancel();
            _listenCycleCts?.Cancel();
            _listenTask?.Wait(TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            Stop();
            _cts.Dispose();
        }

        /// <summary>
        /// Block until the coordinator is stopped.
        /// </summary>
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

                // Create a per-cycle CTS linked to the main one.
                // The pipe watchdog can cancel just this cycle to force socket recreation.
                _listenCycleCts?.Dispose();
                _listenCycleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var cycleToken = _listenCycleCts.Token;

                // Don't use 'using' — ownership transfers to the threadpool handler.
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
                    server.WaitForConnectionAsync(cycleToken).Wait(cycleToken);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    server.Dispose();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Pipe file missing, recreating listener...");
                    continue;
                }
                catch (OperationCanceledException)
                {
                    server.Dispose();
                    break;
                }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException && !ct.IsCancellationRequested)
                {
                    server.Dispose();
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Pipe file missing, recreating listener...");
                    continue;
                }
                catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
                {
                    server.Dispose();
                    break;
                }

                // Handle on threadpool — immediately loop back to accept the next client.
                // This minimizes the gap where no listener is bound.
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

        /// <summary>
        /// Periodic watchdog that detects if the coordinator pipe file was deleted
        /// (e.g. by a cleanup script) and interrupts the listen loop so it recreates it.
        /// </summary>
        private void CheckPipeHealth(object? state)
        {
            string pipeName = EffectivePipeName;
            if (!File.Exists(pipeName))
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] WARNING: Pipe file {pipeName} was deleted externally! Triggering recreation...");
                _listenCycleCts?.Cancel();
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

            string buildId = parts[1];
            if (!int.TryParse(parts[2], out int requested) || requested <= 0)
            {
                writer.WriteLine("ERR invalid requestedNodes");
                return;
            }

            var registration = new BuildRegistration(buildId, requested, DateTime.UtcNow);

            lock (_queueLock)
            {
                // Capacity available — activate immediately
                if (_activeBuilds.Count < _maxConcurrentBuilds)
                {
                    _activeBuilds[buildId] = registration;
                    int granted = CalculateBudget(buildId);
                    registration.GrantedNodes = granted;

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] REGISTER {buildId}: requested={requested} granted={granted} active={_activeBuilds.Count}");
                    writer.WriteLine($"OK {granted}");
                    return;
                }

                // At capacity — queue
                _queuedBuilds.Add(registration);
                int position = _queuedBuilds.Count;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] QUEUED {buildId}: position={position} active={_activeBuilds.Count}");
                writer.WriteLine($"QUEUED {position} {position}");
            }
        }

        private void HandleHeartbeat(string[] parts, StreamWriter writer)
        {
            if (parts.Length < 2)
            {
                writer.WriteLine("ERR usage: HEARTBEAT buildId");
                return;
            }

            string buildId = parts[1];

            // Check if build is active
            if (_activeBuilds.TryGetValue(buildId, out var activeReg))
            {
                activeReg.LastHeartbeat = DateTime.UtcNow;

                int newBudget = CalculateBudget(buildId);
                activeReg.GrantedNodes = newBudget;

                writer.WriteLine($"OK {newBudget}");
                return;
            }

            // Check if build is queued
            lock (_queueLock)
            {
                int index = _queuedBuilds.FindIndex(b => b.BuildId == buildId);
                if (index >= 0)
                {
                    var queuedReg = _queuedBuilds[index];
                    queuedReg.LastHeartbeat = DateTime.UtcNow;
                    int position = index + 1;
                    int totalQueued = _queuedBuilds.Count;
                    int waitSec = (int)(DateTime.UtcNow - queuedReg.QueuedAt).TotalSeconds;
                    writer.WriteLine($"QUEUED {position} {totalQueued} {waitSec}");
                    return;
                }
            }

            // Unknown build — return full budget (fallback)
            writer.WriteLine($"OK {_totalBudget}");
        }

        private void HandleUnregister(string[] parts, StreamWriter writer)
        {
            if (parts.Length < 2)
            {
                writer.WriteLine("ERR usage: UNREGISTER buildId");
                return;
            }

            string buildId = parts[1];

            // Remove from active builds
            bool wasActive = _activeBuilds.TryRemove(buildId, out _);

            // Also remove from queue in case it was queued
            if (!wasActive)
            {
                lock (_queueLock)
                {
                    _queuedBuilds.RemoveAll(b => b.BuildId == buildId);
                }
            }

            int queuedCount;
            lock (_queueLock) { queuedCount = _queuedBuilds.Count; }
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] UNREGISTER {buildId}: active={_activeBuilds.Count} queued={queuedCount}");

            // When a build leaves, promote next in queue if there's a slot.
            string? promoted = null;
            if (wasActive)
            {
                promoted = TryPromoteOne();
            }

            if (promoted != null)
            {
                writer.WriteLine($"OK promoted {promoted}");
            }
            else
            {
                writer.WriteLine("OK");
            }
        }

        /// <summary>
        /// Promote the next queued build if there's capacity. Returns the promoted build ID, or null.
        /// </summary>
        private string? TryPromoteOne()
        {
            if (_activeBuilds.Count >= _maxConcurrentBuilds)
            {
                return null;
            }

            lock (_queueLock)
            {
                if (_queuedBuilds.Count == 0)
                {
                    return null;
                }

                var next = _queuedBuilds[0];
                _queuedBuilds.RemoveAt(0);

                next.PromotedAt = DateTime.UtcNow;
                _activeBuilds[next.BuildId] = next;

                int granted = CalculateBudget(next.BuildId);
                next.GrantedNodes = granted;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] PROMOTED {next.BuildId}: granted={granted} waited={(next.PromotedAt.Value - next.QueuedAt):mm\\:ss} active={_activeBuilds.Count} queued={_queuedBuilds.Count}");

                return next.BuildId;
            }
        }

        private void HandleStatus(StreamWriter writer)
        {
            int queueCount;
            lock (_queueLock) { queueCount = _queuedBuilds.Count; }

            writer.WriteLine($"OK budget={_totalBudget} active={_activeBuilds.Count} queued={queueCount} max={_maxConcurrentBuilds}");

            if (!_activeBuilds.IsEmpty)
            {
                writer.WriteLine("Active:");
                foreach (var kvp in _activeBuilds)
                {
                    var reg = kvp.Value;
                    writer.WriteLine($"  {reg.BuildId}: granted={reg.GrantedNodes} requested={reg.RequestedNodes} age={DateTime.UtcNow - reg.RegisteredAt:mm\\:ss}");
                }
            }

            lock (_queueLock)
            {
                if (_queuedBuilds.Count > 0)
                {
                    writer.WriteLine("Queued:");
                    for (int i = 0; i < _queuedBuilds.Count; i++)
                    {
                        var reg = _queuedBuilds[i];
                        int waitSec = (int)(DateTime.UtcNow - reg.QueuedAt).TotalSeconds;
                        writer.WriteLine($"  #{i + 1} {reg.BuildId}: requested={reg.RequestedNodes} waiting={waitSec}s");
                    }
                }
            }
        }

        private int CalculateBudget(string buildId)
        {
            int activeCount = _activeBuilds.Count;
            if (activeCount == 0)
            {
                return _totalBudget;
            }

            // Account for queued builds that will be promoted soon.
            // This way active builds pre-shrink to make room.
            int pendingCount;
            lock (_queueLock)
            {
                pendingCount = Math.Min(_queuedBuilds.Count, _maxConcurrentBuilds - activeCount);
                pendingCount = Math.Max(0, pendingCount);
            }

            int totalBuilds = Math.Max(_minBuildsForBudget, activeCount + pendingCount);
            int fairShare = Math.Max(1, _totalBudget / totalBuilds);

            // But don't exceed what the build originally requested
            if (_activeBuilds.TryGetValue(buildId, out var registration))
            {
                return Math.Min(fairShare, registration.RequestedNodes);
            }

            return fairShare;
        }

        /// <summary>
        /// Periodic timer callback that removes builds whose process has exited.
        /// Only removes if heartbeat is stale AND the PID is no longer running.
        /// </summary>
        private void ReapStaleBuilds(object? state)
        {
            var now = DateTime.UtcNow;
            bool anyReaped = false;

            // Check active builds
            foreach (var kvp in _activeBuilds)
            {
                var reg = kvp.Value;
                double staleSec = (now - reg.LastHeartbeat).TotalSeconds;

                if (staleSec < (double)StaleHeartbeatSeconds)
                {
                    continue; // Recent heartbeat, still healthy
                }

                // Heartbeat is stale — check if the process is actually dead
                if (IsProcessAlive(kvp.Key))
                {
                    continue; // Process still running, just slow to heartbeat
                }

                // Process is dead — reap it
                if (_activeBuilds.TryRemove(kvp.Key, out _))
                {
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] REAPED {kvp.Key}: process dead, stale {staleSec:F0}s");
                    anyReaped = true;
                }
            }

            // Check queued builds too
            lock (_queueLock)
            {
                for (int i = _queuedBuilds.Count - 1; i >= 0; i--)
                {
                    var reg = _queuedBuilds[i];
                    double staleSec = (now - reg.LastHeartbeat).TotalSeconds;

                    if (staleSec < (double)StaleHeartbeatSeconds)
                    {
                        continue;
                    }

                    if (IsProcessAlive(reg.BuildId))
                    {
                        continue;
                    }

                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] REAPED (queued) {reg.BuildId}: process dead, stale {staleSec:F0}s");
                    _queuedBuilds.RemoveAt(i);
                    anyReaped = true;
                }
            }

            if (anyReaped)
            {
                // Try to promote queued builds into newly opened slots
                while (TryPromoteOne() != null) { }
            }
        }

        /// <summary>
        /// Extract PID from build ID (format: "{PID}-{ticks}") and check if the process is alive.
        /// </summary>
        private static bool IsProcessAlive(string buildId)
        {
            int dashIndex = buildId.IndexOf('-');
            if (dashIndex <= 0)
            {
                return false; // Can't parse — assume dead
            }

            if (!int.TryParse(buildId.AsSpan(0, dashIndex), out int pid))
            {
                return false;
            }

            try
            {
                var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch (ArgumentException)
            {
                // Process doesn't exist
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private sealed class BuildRegistration
        {
            internal string BuildId { get; }
            internal int RequestedNodes { get; }
            internal int GrantedNodes { get; set; }
            internal DateTime RegisteredAt { get; }
            internal DateTime QueuedAt { get; }
            internal DateTime? PromotedAt { get; set; }
            internal DateTime LastHeartbeat { get; set; }
            internal BuildRegistration(string buildId, int requestedNodes, DateTime registeredAt)
            {
                BuildId = buildId;
                RequestedNodes = requestedNodes;
                GrantedNodes = requestedNodes;
                RegisteredAt = registeredAt;
                QueuedAt = registeredAt;
                LastHeartbeat = registeredAt;
            }
        }

        /// <summary>
        /// Check if an existing coordinator is already listening on the given pipe.
        /// Returns true if a coordinator responded; false if the pipe is stale.
        /// </summary>
        private static bool TryConnectToExisting(string pipeName)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, System.IO.Pipes.PipeOptions.CurrentUserOnly);
                client.Connect(500); // 500ms timeout
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
