// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.Coordinator;

/// <summary>
///  Manages the system-wide node budget using a fair-share allocation policy.
///  All public methods are thread-safe.
/// </summary>
internal sealed class NodeBudgetManager
{
    private readonly LockType _lock = new();
    private readonly List<BuildGrant> _activeGrants = [];
    private readonly List<BuildGrant> _waitQueue = [];
    private readonly int _nonHighPriorityBudgetLimit;

    private CoordinatorBuildPriority _highestWaiterRawPriority;
    private int _allocatedNonHighPriorityNodes;

    /// <summary>
    ///  Gets the total node budget available for all builds.
    /// </summary>
    public int TotalBudget { get; }

    /// <summary>
    ///  Gets the number of nodes withheld from low and normal priority grants for high-priority work.
    /// </summary>
    public int HighPriorityReservedNodes { get; }

    /// <summary>
    ///  Gets the maximum number of nodes granted to one build. Zero means uncapped.
    /// </summary>
    public int MaxNodesPerBuild { get; }

    /// <summary>
    ///  Gets the number of bypasses required to age a queued build by one priority level.
    /// </summary>
    public int PriorityAgingThreshold { get; }

    /// <summary>
    ///  Gets the number of nodes currently allocated to active builds.
    /// </summary>
    public int AllocatedNodes { get; private set; }

    /// <summary>
    ///  Gets the number of nodes available for new grants.
    /// </summary>
    public int AvailableNodes => Math.Max(0, TotalBudget - AllocatedNodes);

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
    /// <param name="highPriorityReservedNodes">The number of nodes withheld from low and normal priority grants.</param>
    /// <param name="maxNodesPerBuild">The maximum number of nodes granted to one build. Zero means uncapped.</param>
    /// <param name="priorityAgingThreshold">The number of bypasses required to age a queued build by one priority level.</param>
    public NodeBudgetManager(
        int totalBudget,
        int highPriorityReservedNodes = 0,
        int maxNodesPerBuild = 0,
        int priorityAgingThreshold = CoordinatorSettings.DefaultPriorityAgingThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalBudget, nameof(totalBudget));
        ArgumentOutOfRangeException.ThrowIfNegative(highPriorityReservedNodes, nameof(highPriorityReservedNodes));
        ArgumentOutOfRangeException.ThrowIfNegative(maxNodesPerBuild, nameof(maxNodesPerBuild));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(priorityAgingThreshold, nameof(priorityAgingThreshold));

        TotalBudget = totalBudget;
        HighPriorityReservedNodes = Math.Min(highPriorityReservedNodes, Math.Max(0, totalBudget - 1));
        MaxNodesPerBuild = maxNodesPerBuild == 0 ? 0 : Math.Min(maxNodesPerBuild, totalBudget);
        PriorityAgingThreshold = priorityAgingThreshold;
        _nonHighPriorityBudgetLimit = ComputeNonHighPriorityBudgetLimit();
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
            bool hasWaiters = _waitQueue.Count > 0;
            if (hasWaiters && ShouldQueueBehindWaiters_NoLock(grant))
            {
                QueueGrant_NoLock(grant);
                return 0;
            }

            int available = GetAvailableForGrant(grant);

            if (available <= 0)
            {
                // No resources available. Queue the build.
                QueueGrant_NoLock(grant);
                return 0;
            }

            int grantedNodes = GetGrantableNodes(grant, available);
            if (grantedNodes <= 0)
            {
                QueueGrant_NoLock(grant);
                return 0;
            }

            if (hasWaiters)
            {
                IncrementBypassCounts_NoLock(_waitQueue.Count);
            }

            AddActiveGrant_NoLock(grant, grantedNodes);

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
                    grant.Release();
                }
                else
                {
                    AllocatedNodes -= grant.GrantedNodes;
                    if (grant.Priority != CoordinatorBuildPriority.High)
                    {
                        _allocatedNonHighPriorityNodes -= grant.GrantedNodes;
                    }

                    CoordinatorTelemetry.RecordGrantReleased(grant, WaitingBuildCount, ActiveBuildCount, AllocatedNodes);
                    grant.Release();
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

        while (_waitQueue.Count > 0)
        {
            if (AvailableNodes == 0)
            {
                break;
            }

            int selectedIndex = GetNextGrantIndex_NoLock();
            BuildGrant waiting = _waitQueue[selectedIndex];
            int available = GetAvailableForGrant(waiting);
            int grantedNodes = available > 0 ? GetGrantableNodesForWaiter_NoLock(waiting, available) : 0;
            if (grantedNodes <= 0 && TryGetGrantableHighPriorityWaiter_NoLock(waiting, out int highPriorityIndex, out int highPriorityGrantedNodes))
            {
                selectedIndex = highPriorityIndex;
                waiting = _waitQueue[selectedIndex];
                grantedNodes = highPriorityGrantedNodes;
            }
            else if (grantedNodes <= 0)
            {
                break;
            }

            IncrementBypassCounts_NoLock(selectedIndex);
            RemoveFromWaitQueueAt_NoLock(selectedIndex);

            AddActiveGrant_NoLock(waiting, grantedNodes);
            newlyGranted.Add(waiting);

            CoordinatorTelemetry.RecordDeferredGrantFulfilled(waiting, WaitingBuildCount, ActiveBuildCount, AllocatedNodes);
        }

        return newlyGranted.ToImmutable();
    }

    private int GetAvailableForGrant(BuildGrant grant)
    {
        int totalAvailable = AvailableNodes;
        if (grant.Priority == CoordinatorBuildPriority.High)
        {
            return totalAvailable;
        }

        int nonHighPriorityAvailable = Math.Max(0, GetNonHighPriorityBudgetLimit() - _allocatedNonHighPriorityNodes);
        return Math.Min(totalAvailable, nonHighPriorityAvailable);
    }

    private int GetNonHighPriorityBudgetLimit()
        => _nonHighPriorityBudgetLimit;

    private int ComputeNonHighPriorityBudgetLimit()
    {
        int nonHighPriorityBudget = Math.Max(1, TotalBudget - HighPriorityReservedNodes);
        if (MaxNodesPerBuild <= 0)
        {
            return nonHighPriorityBudget;
        }

        int fullSlices = nonHighPriorityBudget / MaxNodesPerBuild;
        return fullSlices > 0 ? fullSlices * MaxNodesPerBuild : nonHighPriorityBudget;
    }

    private int GetGrantableNodes(BuildGrant grant, int available)
    {
        int maxNodesForGrant = GetMaxNodesForGrant(grant);
        if (maxNodesForGrant <= 0)
        {
            return Math.Min(available, grant.RequestedNodes);
        }

        int desiredNodes = Math.Min(grant.RequestedNodes, maxNodesForGrant);
        return available >= desiredNodes ? desiredNodes : 0;
    }

    private int GetMaxNodesForGrant(BuildGrant grant)
    {
        if (MaxNodesPerBuild <= 0)
        {
            return 0;
        }

        if (grant.Priority == CoordinatorBuildPriority.High)
        {
            return MaxNodesPerBuild;
        }

        return Math.Min(MaxNodesPerBuild, Math.Max(1, TotalBudget - HighPriorityReservedNodes));
    }

    private int GetGrantableNodesForWaiter_NoLock(BuildGrant grant, int available)
    {
        if (MaxNodesPerBuild > 0)
        {
            return GetGrantableNodes(grant, available);
        }

        CoordinatorBuildPriority selectedPriority = GetEffectivePriority(grant);
        int contenders = CountWaitersWithEffectivePriority_NoLock(selectedPriority);
        int fairShare = Math.Max(1, available / contenders);
        return Math.Min(fairShare, grant.RequestedNodes);
    }

    private void RemoveFromWaitQueue_NoLock(BuildGrant grant)
    {
        if (_waitQueue.Remove(grant) && grant.Priority == _highestWaiterRawPriority)
        {
            RefreshHighestWaiterRawPriority_NoLock();
        }
    }

    private void RemoveFromWaitQueueAt_NoLock(int index)
    {
        BuildGrant grant = _waitQueue[index];
        _waitQueue.RemoveAt(index);

        if (grant.Priority == _highestWaiterRawPriority)
        {
            RefreshHighestWaiterRawPriority_NoLock();
        }
    }

    private void QueueGrant_NoLock(BuildGrant grant)
    {
        if (_waitQueue.Count == 0 || grant.Priority > _highestWaiterRawPriority)
        {
            _highestWaiterRawPriority = grant.Priority;
        }

        _waitQueue.Add(grant);
        CoordinatorTelemetry.RecordGrantDeferred(grant, WaitingBuildCount);
    }

    private void AddActiveGrant_NoLock(BuildGrant grant, int grantedNodes)
    {
        grant.Activate(grantedNodes);
        AllocatedNodes += grantedNodes;
        if (grant.Priority != CoordinatorBuildPriority.High)
        {
            _allocatedNonHighPriorityNodes += grantedNodes;
        }

        _activeGrants.Add(grant);
    }

    private void RefreshHighestWaiterRawPriority_NoLock()
    {
        _highestWaiterRawPriority = CoordinatorBuildPriority.Low;

        foreach (BuildGrant grant in _waitQueue)
        {
            if (grant.Priority > _highestWaiterRawPriority)
            {
                _highestWaiterRawPriority = grant.Priority;
            }
        }
    }

    private bool ShouldQueueBehindWaiters_NoLock(BuildGrant grant)
    {
        if (grant.Priority == CoordinatorBuildPriority.High)
        {
            if (grant.Priority <= _highestWaiterRawPriority)
            {
                return true;
            }

            if (grant.Priority > GetHighestEffectivePriority_NoLock())
            {
                return false;
            }

            // Aged Low/Normal waiters can have High effective priority, but they still cannot
            // consume high-priority reserved capacity. Let real High arrivals bypass only when
            // they can use capacity that is unavailable to those aged waiters.
            return !CanUseCapacityUnavailableToNonHighPriorityWaiters_NoLock(grant);
        }

        return grant.Priority <= GetHighestEffectivePriority_NoLock();
    }

    private int GetNextGrantIndex_NoLock()
    {
        int selectedIndex = 0;
        CoordinatorBuildPriority selectedPriority = GetEffectivePriority(_waitQueue[0]);

        for (int i = 1; i < _waitQueue.Count; i++)
        {
            CoordinatorBuildPriority candidatePriority = GetEffectivePriority(_waitQueue[i]);

            if (candidatePriority > selectedPriority)
            {
                selectedIndex = i;
                selectedPriority = candidatePriority;
            }
        }

        return selectedIndex;
    }

    private CoordinatorBuildPriority GetHighestEffectivePriority_NoLock()
    {
        CoordinatorBuildPriority highestPriority = GetEffectivePriority(_waitQueue[0]);

        for (int i = 1; i < _waitQueue.Count; i++)
        {
            CoordinatorBuildPriority candidatePriority = GetEffectivePriority(_waitQueue[i]);
            if (candidatePriority > highestPriority)
            {
                highestPriority = candidatePriority;
            }
        }

        return highestPriority;
    }

    private int CountWaitersWithEffectivePriority_NoLock(CoordinatorBuildPriority priority)
    {
        int count = 0;

        foreach (BuildGrant grant in _waitQueue)
        {
            if (GetEffectivePriority(grant) == priority)
            {
                count++;
            }
        }

        return count;
    }

    private bool TryGetGrantableHighPriorityWaiter_NoLock(BuildGrant skippedWaiter, out int selectedIndex, out int grantedNodes)
    {
        selectedIndex = -1;
        grantedNodes = 0;

        if (skippedWaiter.Priority == CoordinatorBuildPriority.High || _highestWaiterRawPriority < CoordinatorBuildPriority.High)
        {
            return false;
        }

        for (int i = 0; i < _waitQueue.Count; i++)
        {
            BuildGrant candidate = _waitQueue[i];
            if (candidate.Priority != CoordinatorBuildPriority.High)
            {
                continue;
            }

            int available = GetAvailableForGrant(candidate);
            if (available <= 0 || !CanUseCapacityUnavailableToNonHighPriorityWaiters_NoLock(candidate, available))
            {
                return false;
            }

            int candidateGrantedNodes = GetGrantableNodesForHighPriorityFallbackWaiter_NoLock(candidate, available);
            if (candidateGrantedNodes > 0)
            {
                selectedIndex = i;
                grantedNodes = candidateGrantedNodes;
                return true;
            }

            return false;
        }

        return false;
    }

    private bool CanUseCapacityUnavailableToNonHighPriorityWaiters_NoLock(BuildGrant highPriorityGrant)
        => CanUseCapacityUnavailableToNonHighPriorityWaiters_NoLock(highPriorityGrant, GetAvailableForGrant(highPriorityGrant));

    private bool CanUseCapacityUnavailableToNonHighPriorityWaiters_NoLock(BuildGrant highPriorityGrant, int available)
    {
        if (highPriorityGrant.Priority != CoordinatorBuildPriority.High || available <= 0)
        {
            return false;
        }

        int nonHighPriorityAvailable = Math.Max(0, GetNonHighPriorityBudgetLimit() - _allocatedNonHighPriorityNodes);
        return available > nonHighPriorityAvailable;
    }

    private int GetGrantableNodesForHighPriorityFallbackWaiter_NoLock(BuildGrant grant, int available)
    {
        if (MaxNodesPerBuild > 0)
        {
            return GetGrantableNodes(grant, available);
        }

        int contenders = Math.Max(1, CountRawHighPriorityWaiters_NoLock());
        int fairShare = Math.Max(1, available / contenders);
        return Math.Min(fairShare, grant.RequestedNodes);
    }

    private int CountRawHighPriorityWaiters_NoLock()
    {
        int count = 0;

        foreach (BuildGrant grant in _waitQueue)
        {
            if (grant.Priority == CoordinatorBuildPriority.High)
            {
                count++;
            }
        }

        return count;
    }

    private void IncrementBypassCounts_NoLock(int selectedIndex)
    {
        for (int i = 0; i < selectedIndex; i++)
        {
            _waitQueue[i].BypassCount++;
        }
    }

    private CoordinatorBuildPriority GetEffectivePriority(BuildGrant grant)
        => (CoordinatorBuildPriority)Math.Min(
            (int)CoordinatorBuildPriority.High,
            (int)grant.Priority + (grant.BypassCount / PriorityAgingThreshold));
}
