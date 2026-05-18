// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Outcome of a <see cref="BuildCoordinator.Register"/> call.
    /// </summary>
    public enum RegisterOutcome
    {
        /// <summary>Build was activated immediately.</summary>
        Granted,
        /// <summary>Build was queued — wait for promotion.</summary>
        Queued,
    }

    /// <summary>
    /// Result returned from <see cref="BuildCoordinator.Register"/>.
    /// </summary>
    public readonly struct RegisterResult
    {
        public RegisterOutcome Outcome { get; }
        public int GrantedNodes { get; }
        public int QueuePosition { get; }
        public int QueueTotal { get; }

        private RegisterResult(RegisterOutcome outcome, int grantedNodes, int queuePosition, int queueTotal)
        {
            Outcome = outcome;
            GrantedNodes = grantedNodes;
            QueuePosition = queuePosition;
            QueueTotal = queueTotal;
        }

        internal static RegisterResult Grant(int grantedNodes) => new(RegisterOutcome.Granted, grantedNodes, 0, 0);
        internal static RegisterResult Queue(int position, int total) => new(RegisterOutcome.Queued, 0, position, total);
    }

    /// <summary>
    /// Snapshot of coordinator state returned by <see cref="BuildCoordinator.GetStatus"/>.
    /// </summary>
    public readonly struct CoordinatorStatus
    {
        public int TotalBudget { get; }
        public int ActiveCount { get; }
        public int QueuedCount { get; }
        public int MaxConcurrentBuilds { get; }

        internal CoordinatorStatus(int totalBudget, int activeCount, int queuedCount, int maxConcurrentBuilds)
        {
            TotalBudget = totalBudget;
            ActiveCount = activeCount;
            QueuedCount = queuedCount;
            MaxConcurrentBuilds = maxConcurrentBuilds;
        }
    }

    /// <summary>
    /// Pure domain coordinator that manages node budgets across concurrent MSBuild instances.
    ///
    /// Simple capacity model: builds register and are activated immediately if there's capacity
    /// (active count &lt; maxConcurrentBuilds). Otherwise they're queued FIFO and promoted when
    /// a slot opens (via Unregister or staleness reaping). Each active build is granted a fair
    /// share of the total node budget (totalBudget / activeCount), capped at what it requested.
    ///
    /// This class contains NO I/O. Transport is handled by <see cref="NamedPipeCoordinatorHost"/>.
    /// </summary>
    public sealed class BuildCoordinator : IDisposable
    {
        private readonly INodeBudgetPolicy _scheduler;
        private readonly int _startupDelayMs;
        private readonly ConcurrentDictionary<string, BuildRegistration> _activeBuilds = new();
        private readonly List<BuildRegistration> _queuedBuilds = new();
        private readonly object _queueLock = new();
        private Timer? _stalenessReaper;
        private bool _startupDelayActive;
        private Timer? _startupDelayTimer;

        /// <summary>
        /// If a build hasn't heartbeated in this many seconds, consider it dead.
        /// </summary>
        internal int StaleHeartbeatSeconds { get; set; } = 10;

        /// <summary>
        /// How often the staleness reaper runs (in seconds).
        /// </summary>
        internal int ReaperIntervalSeconds { get; set; } = 5;

        /// <summary>
        /// The budget policy used by this coordinator.
        /// </summary>
        internal INodeBudgetPolicy Scheduler => _scheduler;

        /// <summary>
        /// Callback invoked when a queued build is promoted to active.
        /// The transport layer provides this at construction to receive promotion notifications.
        /// Parameters: (buildId, grantedNodes).
        /// </summary>
        private readonly Action<string, int>? _onBuildPromoted;

        public BuildCoordinator(INodeBudgetPolicy scheduler, int startupDelayMs = 0, Action<string, int>? onBuildPromoted = null)
        {
            _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            _startupDelayMs = Math.Max(0, startupDelayMs);
            _startupDelayActive = _startupDelayMs > 0;
            _onBuildPromoted = onBuildPromoted;
        }

        /// <summary>
        /// Register a build. Returns immediately with either a grant (active) or a queue position.
        /// </summary>
        public RegisterResult Register(string buildId, int requestedNodes)
        {
            var registration = new BuildRegistration(buildId, requestedNodes, DateTime.UtcNow);

            lock (_queueLock)
            {
                // Startup delay: queue everything and start a one-shot timer to batch-promote.
                if (_startupDelayActive)
                {
                    _queuedBuilds.Add(registration);
                    int position = _queuedBuilds.Count;

                    if (_startupDelayTimer == null)
                    {
                        _startupDelayTimer = new Timer(OnStartupDelayElapsed, null, _startupDelayMs, Timeout.Infinite);
                    }

                    return RegisterResult.Queue(position, position);
                }

                // If there are already queued builds, new arrivals must queue too (FIFO).
                if (_queuedBuilds.Count > 0)
                {
                    _queuedBuilds.Add(registration);
                    int position = _queuedBuilds.Count;
                    return RegisterResult.Queue(position, position);
                }

                // No queue — check concurrency limit before admitting.
                if (_activeBuilds.Count < _scheduler.MaxConcurrentBuilds)
                {
                    _activeBuilds[buildId] = registration;
                    int allocated = SumAllocatedNodes();
                    int granted = _scheduler.GetGrantedNodes(requestedNodes, _activeBuilds.Count, _queuedBuilds.Count, allocated);
                    registration.GrantedNodes = granted;
                    return RegisterResult.Grant(granted);
                }

                // At capacity — queue.
                _queuedBuilds.Add(registration);
                int queuePosition = _queuedBuilds.Count;
                return RegisterResult.Queue(queuePosition, queuePosition);
            }
        }

        /// <summary>
        /// Record a heartbeat for the given build.
        /// </summary>
        public void Heartbeat(string buildId)
        {
            if (_activeBuilds.TryGetValue(buildId, out var reg))
            {
                reg.LastHeartbeat = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Unregister a build and promote queued builds into any newly opened slots.
        /// Returns the number of builds promoted.
        /// </summary>
        public int Unregister(string buildId)
        {
            bool wasActive = _activeBuilds.TryRemove(buildId, out _);

            if (!wasActive)
            {
                lock (_queueLock)
                {
                    _queuedBuilds.RemoveAll(b => b.BuildId == buildId);
                }
            }

            return wasActive ? PromoteAllPossible() : 0;
        }

        /// <summary>
        /// Returns a snapshot of the current coordinator state.
        /// </summary>
        public CoordinatorStatus GetStatus()
        {
            int queueCount;
            lock (_queueLock) { queueCount = _queuedBuilds.Count; }
            return new CoordinatorStatus(_scheduler.TotalBudget, _activeBuilds.Count, queueCount, _scheduler.MaxConcurrentBuilds);
        }

        /// <summary>
        /// Start the periodic staleness reaper timer.
        /// </summary>
        public void StartReaper()
        {
            _stalenessReaper?.Dispose();
            _stalenessReaper = new Timer(ReapStaleBuilds, null, TimeSpan.FromSeconds(ReaperIntervalSeconds), TimeSpan.FromSeconds(ReaperIntervalSeconds));
        }

        public void Dispose()
        {
            _stalenessReaper?.Dispose();
            _stalenessReaper = null;
            _startupDelayTimer?.Dispose();
            _startupDelayTimer = null;
        }

        /// <summary>
        /// Called when the startup delay timer fires. Ends the delay phase and batch-promotes
        /// up to maxConcurrentBuilds from the queue.
        /// </summary>
        private void OnStartupDelayElapsed(object? state)
        {
            lock (_queueLock)
            {
                _startupDelayTimer?.Dispose();
                _startupDelayTimer = null;
                _startupDelayActive = false;
            }

            PromoteAllPossible();
        }

        /// <summary>
        /// Promote as many queued builds as capacity allows. Returns the number promoted.
        /// </summary>
        private int PromoteAllPossible()
        {
            int count = 0;
            while (TryPromoteOne() != null)
            {
                count++;
            }

            return count;
        }

        /// <summary>
        /// Promote the next queued build if there's capacity. Returns the promoted build ID, or null.
        /// Invokes the promotion callback to notify the transport layer.
        /// </summary>
        private string? TryPromoteOne()
        {
            string buildId;
            int granted;

            lock (_queueLock)
            {
                if (_activeBuilds.Count >= _scheduler.MaxConcurrentBuilds)
                {
                    return null;
                }

                if (_queuedBuilds.Count == 0)
                {
                    return null;
                }

                var next = _queuedBuilds[0];
                _queuedBuilds.RemoveAt(0);

                _activeBuilds[next.BuildId] = next;

                int allocated = SumAllocatedNodes();
                granted = _scheduler.GetGrantedNodes(next.RequestedNodes, _activeBuilds.Count, _queuedBuilds.Count, allocated);
                next.GrantedNodes = granted;
                buildId = next.BuildId;
            }

            _onBuildPromoted?.Invoke(buildId, granted);
            return buildId;
        }

        /// <summary>
        /// Periodic timer callback that removes builds whose process has exited.
        /// Only removes active builds if heartbeat is stale AND the PID is no longer running.
        /// Queued builds are checked by process liveness only.
        /// </summary>
        private void ReapStaleBuilds(object? state)
        {
            var now = DateTime.UtcNow;
            bool anyReaped = false;

            // Snapshot keys to avoid mutating the dictionary during iteration.
            var staleKeys = new List<string>();
            foreach (var kvp in _activeBuilds)
            {
                double staleSec = (now - kvp.Value.LastHeartbeat).TotalSeconds;
                if (staleSec >= (double)StaleHeartbeatSeconds && !IsProcessAlive(kvp.Key))
                {
                    staleKeys.Add(kvp.Key);
                }
            }

            foreach (var key in staleKeys)
            {
                if (_activeBuilds.TryRemove(key, out _))
                {
                    anyReaped = true;
                }
            }

            lock (_queueLock)
            {
                for (int i = _queuedBuilds.Count - 1; i >= 0; i--)
                {
                    if (!IsProcessAlive(_queuedBuilds[i].BuildId))
                    {
                        _queuedBuilds.RemoveAt(i);
                        anyReaped = true;
                    }
                }
            }

            if (anyReaped)
            {
                PromoteAllPossible();
            }
        }

        /// <summary>Sum of GrantedNodes across all active builds (newly added build has 0).</summary>
        private int SumAllocatedNodes()
        {
            int sum = 0;
            foreach (var kvp in _activeBuilds)
            {
                sum += kvp.Value.GrantedNodes;
            }

            return sum;
        }

        /// <summary>
        /// Extract PID from build ID (format: "{PID}-{ticks}") and check if the process is alive.
        /// </summary>
        private static bool IsProcessAlive(string buildId)
        {
            int dashIndex = buildId.IndexOf('-');
            if (dashIndex <= 0)
            {
                return false;
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
            internal DateTime LastHeartbeat { get; set; }

            internal BuildRegistration(string buildId, int requestedNodes, DateTime now)
            {
                BuildId = buildId;
                RequestedNodes = requestedNodes;
                GrantedNodes = 0;
                LastHeartbeat = now;
            }
        }
    }
}
