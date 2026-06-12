// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Collections;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  Manages the system-wide node budget using a fair-share allocation policy.
///  All public methods must be called under external synchronization.
/// </summary>
internal sealed class NodeBudgetManager
{
    private readonly List<BuildGrant> _activeGrants = [];
    private readonly Queue<BuildGrant> _waitQueue = new();

    /// <summary>
    ///  The total node budget available for all builds.
    /// </summary>
    public int TotalBudget { get; }

    /// <summary>
    ///  The number of nodes currently allocated to active builds.
    /// </summary>
    public int AllocatedNodes { get; private set; }

    /// <summary>
    ///  The number of nodes available for new grants.
    /// </summary>
    public int AvailableNodes => TotalBudget - AllocatedNodes;

    /// <summary>
    ///  The number of active builds (those with grants).
    /// </summary>
    public int ActiveBuildCount => _activeGrants.Count;

    /// <summary>
    ///  The number of builds waiting in the queue.
    /// </summary>
    public int WaitingBuildCount => _waitQueue.Count;

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

        int available = AvailableNodes;

        if (available <= 0)
        {
            // No resources available. Queue the build.
            _waitQueue.Enqueue(grant);
            CoordinatorTelemetry.RecordGrantDeferred(grant.ConnectionId, grant.ProcessId, grant.RequestedNodes, WaitingBuildCount);
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

        CoordinatorTelemetry.RecordGrantIssued(grant.ConnectionId, grant.ProcessId, grant.RequestedNodes, grantedNodes, WaitingBuildCount, ActiveBuildCount, AllocatedNodes);

        return grantedNodes;
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
        if (grant.IsActive)
        {
            AllocatedNodes -= grant.GrantedNodes;
            CoordinatorTelemetry.RecordGrantReleased(grant.ConnectionId, grant.ProcessId, grant.GrantedNodes, WaitingBuildCount, ActiveBuildCount, AllocatedNodes);
            grant.GrantedNodes = 0;
            _activeGrants.Remove(grant);
        }
        else
        {
            // The build was still in the wait queue.
            RemoveFromWaitQueue(grant);
        }

        return DrainWaitQueue();
    }

    /// <summary>
    ///  Processes the wait queue, granting nodes to as many waiting builds as possible
    ///  using fair-share allocation.
    /// </summary>
    /// <returns>
    ///  An array of grants that were newly fulfilled from the wait queue.
    /// </returns>
    private ImmutableArray<BuildGrant> DrainWaitQueue()
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

            CoordinatorTelemetry.RecordDeferredGrantFulfilled(waiting.ConnectionId, waiting.ProcessId, grantedNodes, WaitingBuildCount, ActiveBuildCount, AllocatedNodes);
        }

        return newlyGranted.ToImmutable();
    }

    private void RemoveFromWaitQueue(BuildGrant grant)
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
