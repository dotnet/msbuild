// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  Manages the system-wide node budget using a fair-share allocation policy.
///  All public methods are thread-safe.
/// </summary>
internal sealed class NodeBudgetManager
{
    private readonly LockType _lock = new();
    private readonly List<BuildGrant> _activeGrants = [];
    private readonly Queue<BuildGrant> _waitQueue = new();

    /// <summary>
    ///  Gets the total node budget available for all builds.
    /// </summary>
    public int TotalBudget { get; }

    /// <summary>
    ///  Gets the number of nodes currently allocated to active builds.
    /// </summary>
    public int AllocatedNodes { get; private set; }

    /// <summary>
    ///  Gets the number of nodes available for new grants.
    /// </summary>
    public int AvailableNodes => TotalBudget - AllocatedNodes;

    /// <summary>
    ///  Gets the number of active builds (those with grants).
    /// </summary>
    public int ActiveBuildCount => _activeGrants.Count;

    /// <summary>
    ///  Gets the number of builds waiting in the queue.
    /// </summary>
    public int WaitingBuildCount => _waitQueue.Count;

    /// <summary>
    ///  Returns <see langword="true"/> if there are no active or waiting builds.
    /// </summary>
    public bool IsIdle
    {
        get
        {
            lock (_lock)
            {
                return _activeGrants.Count == 0 && _waitQueue.Count == 0;
            }
        }
    }

    /// <summary>
    ///  Creates a new budget manager with the specified total node capacity.
    /// </summary>
    /// <param name="totalBudget">The maximum number of nodes available across all builds.</param>
    public NodeBudgetManager(int totalBudget)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalBudget, nameof(totalBudget));

        TotalBudget = totalBudget;
    }

    /// <summary>
    ///  Attempts to grant nodes to a build using fair-share allocation.
    ///  Returns the number of nodes granted, or zero if the build must wait.
    /// </summary>
    /// <param name="grant">The build grant to allocate nodes for.</param>
    /// <returns>
    ///  The number of nodes granted, or zero if no resources are available and the build was queued.
    /// </returns>
    public int TryGrant(BuildGrant grant)
    {
        if (grant.RequestedNodes <= 0)
        {
            return 0;
        }

        if (grant.IsNested)
        {
            Assumed.Positive(grant.GrantedNodes);
            CoordinatorTelemetry.RecordGrantIssued(grant, WaitingBuildCount, ActiveBuildCount, AllocatedNodes);
            return grant.GrantedNodes;
        }

        lock (_lock)
        {
            int available = AvailableNodes;

            if (available <= 0)
            {
                // No resources available. Queue the build.
                _waitQueue.Enqueue(grant);
                CoordinatorTelemetry.RecordGrantDeferred(grant, WaitingBuildCount);
                return 0;
            }

            // Fair share: divide available budget among this build and all waiting builds.
            // This ensures a new arrival doesn't consume everything while others wait.
            int contenders = _waitQueue.Count + 1; // +1 for the new arrival
            int fairShare = Math.Max(1, available / contenders);
            int grantedNodes = Math.Min(fairShare, grant.RequestedNodes);

            grant.GrantedNodes = grantedNodes;
            AllocatedNodes += grantedNodes;
            _activeGrants.Add(grant);

            CoordinatorTelemetry.RecordGrantIssued(grant, WaitingBuildCount, ActiveBuildCount, AllocatedNodes);

            return grantedNodes;
        }
    }

    /// <summary>
    ///  Releases a build's grant and returns any builds from the wait queue
    ///  that can now be granted nodes.
    /// </summary>
    /// <param name="grant">The build grant to release.</param>
    /// <returns>
    ///  An array of grants that were fulfilled from the wait queue as a result of the release.
    /// </returns>
    public ImmutableArray<BuildGrant> Release(BuildGrant grant)
    {
        lock (_lock)
        {
            if (grant.IsActive)
            {
                if (grant.IsNested)
                {
                    CoordinatorTelemetry.RecordGrantReleased(grant, WaitingBuildCount, ActiveBuildCount, AllocatedNodes);
                    grant.GrantedNodes = 0;
                }
                else
                {
                    AllocatedNodes -= grant.GrantedNodes;
                    CoordinatorTelemetry.RecordGrantReleased(grant, WaitingBuildCount, ActiveBuildCount, AllocatedNodes);
                    grant.GrantedNodes = 0;
                    _activeGrants.Remove(grant);
                }
            }
            else
            {
                // The build was still in the wait queue.
                RemoveFromWaitQueue_NoLock(grant);
            }

            return DrainWaitQueue_NoLock();
        }
    }

    /// <summary>
    ///  Processes the wait queue, granting nodes to as many waiting builds as possible
    ///  using fair-share allocation.
    /// </summary>
    /// <returns>
    ///  An array of grants that were newly fulfilled from the wait queue.
    /// </returns>
    private ImmutableArray<BuildGrant> DrainWaitQueue_NoLock()
    {
        using RefArrayBuilder<BuildGrant> newlyGranted = new();

        while (_waitQueue.Count > 0 && AvailableNodes > 0)
        {
            int available = AvailableNodes;
            int contenders = _waitQueue.Count;
            int fairShare = Math.Max(1, available / contenders);

            BuildGrant waiting = _waitQueue.Dequeue();
            int grantedNodes = Math.Min(fairShare, waiting.RequestedNodes);

            waiting.GrantedNodes = grantedNodes;
            AllocatedNodes += grantedNodes;
            _activeGrants.Add(waiting);
            newlyGranted.Add(waiting);

            CoordinatorTelemetry.RecordDeferredGrantFulfilled(waiting, WaitingBuildCount, ActiveBuildCount, AllocatedNodes);
        }

        return newlyGranted.ToImmutable();
    }

    private void RemoveFromWaitQueue_NoLock(BuildGrant grant)
    {
        // Queue<T> doesn't support removal, so rebuild it.
        int count = _waitQueue.Count;

        for (int i = 0; i < count; i++)
        {
            BuildGrant queued = _waitQueue.Dequeue();

            if (queued != grant)
            {
                _waitQueue.Enqueue(queued);
            }
        }
    }
}
