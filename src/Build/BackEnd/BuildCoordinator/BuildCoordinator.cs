// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// A standalone coordinator process that manages node budgets across concurrent MSBuild instances.
    ///
    /// Key design: heartbeat-gated promotion. When a new build registers and there are already
    /// active builds, the new build is queued. Active builds learn their reduced budget on their
    /// next heartbeat. Only after ALL active builds have acknowledged the reduction (via heartbeat)
    /// is the new build promoted. This prevents temporarily exceeding the node budget.
    ///
    /// Protocol (line-based text over named pipe):
    ///   REGISTER buildId requestedNodes
    ///     → OK grantedNodes                    (first build — immediate)
    ///     → QUEUED position totalQueued         (subsequent builds — wait for heartbeat gate)
    ///
    ///   HEARTBEAT buildId
    ///     → OK grantedNodes                    (active build — may have new budget)
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
        /// Well-known pipe name. All MSBuild instances for this user connect here.
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
        private readonly ConcurrentDictionary<string, BuildRegistration> _activeBuilds = new();
        private readonly List<BuildRegistration> _queuedBuilds = new();
        private readonly object _queueLock = new();
        private readonly CancellationTokenSource _cts = new();
        private Task? _listenTask;
        private Timer? _stalenessReaper;
        private Timer? _pipeWatchdog;
        private CancellationTokenSource? _listenCycleCts;

        /// <summary>
        /// Epoch counter — bumped whenever the budget landscape changes and active builds
        /// need to acknowledge their new budget before queued builds can be promoted.
        /// </summary>
        private int _rebalanceEpoch;

        /// <summary>
        /// If a build hasn't heartbeated in this many seconds, consider it dead.
        /// </summary>
        private const int StaleHeartbeatSeconds = 10;

        public BuildCoordinator(int totalBudget, int maxConcurrentBuilds, int minBuildsForBudget = 1)
        {
            _totalBudget = totalBudget;
            _maxConcurrentBuilds = maxConcurrentBuilds;
            _minBuildsForBudget = Math.Max(1, minBuildsForBudget);
        }

        /// <summary>
        /// Start listening for MSBuild client connections.
        /// </summary>
        public void Start()
        {
            string pipeName = GetPipeName();

            // On Unix, clean up stale pipe file
            if (NativeMethodsShared.IsUnixLike && File.Exists(pipeName))
            {
                File.Delete(pipeName);
            }

            Console.WriteLine($"Build Coordinator starting");
            Console.WriteLine($"  Pipe: {pipeName}");
            Console.WriteLine($"  Budget: {_totalBudget} nodes");
            Console.WriteLine($"  Max concurrent builds: {_maxConcurrentBuilds}");
            Console.WriteLine($"  Min builds for budget: {_minBuildsForBudget}");

            _listenTask = Task.Run(() => ListenLoop(_cts.Token));

            // Periodically reap builds that stopped heartbeating (crashed/killed process)
            _stalenessReaper = new Timer(ReapStaleBuilds, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

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
                string pipeName = GetPipeName();

                // Create a per-cycle CTS linked to the main one.
                // The pipe watchdog can cancel just this cycle to force socket recreation.
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
            string pipeName = GetPipeName();
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

            // First build ever — activate immediately with full budget
            if (_activeBuilds.IsEmpty)
            {
                _activeBuilds[buildId] = registration;
                registration.AcknowledgedEpoch = _rebalanceEpoch;
                int granted = CalculateBudget(buildId);
                registration.GrantedNodes = granted;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] REGISTER {buildId}: requested={requested} granted={granted} (first build)");
                writer.WriteLine($"OK {granted}");
                return;
            }

            // Subsequent builds — always queue. Bump epoch so active builds must
            // heartbeat (acknowledge reduced budget) before this build is promoted.
            lock (_queueLock)
            {
                _queuedBuilds.Add(registration);
                _rebalanceEpoch++;
                int position = _queuedBuilds.Count;
                int totalQueued = _queuedBuilds.Count;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] QUEUED {buildId}: position={position}/{totalQueued} active={_activeBuilds.Count} epoch={_rebalanceEpoch} (waiting for heartbeat gate)");
                writer.WriteLine($"QUEUED {position} {totalQueued}");
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
                activeReg.AcknowledgedEpoch = _rebalanceEpoch;

                int newBudget = CalculateBudget(buildId);
                activeReg.GrantedNodes = newBudget;
                writer.WriteLine($"OK {newBudget}");

                // After acknowledging, check if all active builds are caught up
                // and we can promote queued builds
                TryPromotePending();
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

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] UNREGISTER {buildId}: active={_activeBuilds.Count} queued={_queuedBuilds.Count}");

            // When a build leaves, remaining builds get MORE budget (safe direction).
            // Promote immediately if there's a slot — the promoted build gets the correct
            // share, and existing builds will learn their increased budget on next heartbeat.
            string? promoted = null;
            if (wasActive && _activeBuilds.Count < _maxConcurrentBuilds)
            {
                lock (_queueLock)
                {
                    if (_queuedBuilds.Count > 0)
                    {
                        var next = _queuedBuilds[0];
                        _queuedBuilds.RemoveAt(0);
                        next.PromotedAt = DateTime.UtcNow;
                        next.AcknowledgedEpoch = _rebalanceEpoch;
                        _activeBuilds[next.BuildId] = next;
                        int granted = CalculateBudget(next.BuildId);
                        next.GrantedNodes = granted;
                        promoted = next.BuildId;

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] PROMOTED {next.BuildId}: granted={granted} waited={(next.PromotedAt.Value - next.QueuedAt):mm\\:ss} active={_activeBuilds.Count} queued={_queuedBuilds.Count}");

                        // If more queued, bump epoch for next round
                        if (_queuedBuilds.Count > 0)
                        {
                            _rebalanceEpoch++;
                        }
                    }
                }
            }

            if (promoted != null)
            {
                writer.WriteLine($"OK promoted {promoted}");
            }
            else
            {
                writer.WriteLine("OK");
            }

            RebalanceAll();
        }

        /// <summary>
        /// Promote queued builds if:
        ///   1. There's capacity (active &lt; max concurrent)
        ///   2. All active builds have acknowledged the current rebalance epoch
        ///      (so they've received their reduced budget via heartbeat)
        /// Promotes one build at a time, bumping epoch after each so the newly
        /// promoted build must also heartbeat before the next one is promoted.
        /// </summary>
        private void TryPromotePending()
        {
            if (_activeBuilds.Count >= _maxConcurrentBuilds)
            {
                return;
            }

            // Check that ALL active builds have acknowledged the current epoch
            int currentEpoch = _rebalanceEpoch;
            foreach (var kvp in _activeBuilds)
            {
                if (kvp.Value.AcknowledgedEpoch < currentEpoch)
                {
                    return; // Not all caught up yet
                }
            }

            lock (_queueLock)
            {
                if (_queuedBuilds.Count == 0)
                {
                    return;
                }

                // Promote one build
                var next = _queuedBuilds[0];
                _queuedBuilds.RemoveAt(0);

                next.PromotedAt = DateTime.UtcNow;
                next.AcknowledgedEpoch = currentEpoch; // It starts caught up
                _activeBuilds[next.BuildId] = next;

                int granted = CalculateBudget(next.BuildId);
                next.GrantedNodes = granted;

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] PROMOTED {next.BuildId}: granted={granted} waited={(next.PromotedAt.Value - next.QueuedAt):mm\\:ss} active={_activeBuilds.Count} queued={_queuedBuilds.Count}");

                // If more queued, bump epoch — existing active builds must heartbeat
                // their new (further-reduced) budget before the next promotion
                if (_queuedBuilds.Count > 0)
                {
                    _rebalanceEpoch++;
                    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Epoch bumped to {_rebalanceEpoch} — {_queuedBuilds.Count} still queued");
                }
            }

            RebalanceAll();
        }

        private void HandleStatus(StreamWriter writer)
        {
            int queueCount;
            lock (_queueLock) { queueCount = _queuedBuilds.Count; }

            writer.WriteLine($"OK budget={_totalBudget} active={_activeBuilds.Count} queued={queueCount} max={_maxConcurrentBuilds} epoch={_rebalanceEpoch}");

            if (!_activeBuilds.IsEmpty)
            {
                writer.WriteLine("Active:");
                foreach (var kvp in _activeBuilds)
                {
                    var reg = kvp.Value;
                    string ack = reg.AcknowledgedEpoch >= _rebalanceEpoch ? "yes" : "no";
                    writer.WriteLine($"  {reg.BuildId}: granted={reg.GrantedNodes} requested={reg.RequestedNodes} epoch_ack={ack} age={DateTime.UtcNow - reg.RegisteredAt:mm\\:ss}");
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

        private void RebalanceAll()
        {
            foreach (var kvp in _activeBuilds)
            {
                kvp.Value.GrantedNodes = CalculateBudget(kvp.Key);
            }

            if (!_activeBuilds.IsEmpty)
            {
                var summary = string.Join(", ", _activeBuilds.Select(b => $"{b.Key}={b.Value.GrantedNodes}"));
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Rebalanced: {summary}");
            }
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

                if (staleSec < StaleHeartbeatSeconds)
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

                    if (staleSec < StaleHeartbeatSeconds)
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
                TryPromotePending();
                RebalanceAll();
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
            internal int AcknowledgedEpoch { get; set; }

            internal BuildRegistration(string buildId, int requestedNodes, DateTime registeredAt)
            {
                BuildId = buildId;
                RequestedNodes = requestedNodes;
                GrantedNodes = requestedNodes;
                RegisteredAt = registeredAt;
                QueuedAt = registeredAt;
                LastHeartbeat = registeredAt;
                AcknowledgedEpoch = -1; // Not yet acknowledged
            }
        }
    }
}
