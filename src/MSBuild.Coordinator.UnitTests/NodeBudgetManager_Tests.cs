// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class NodeBudgetManager_Tests
{
    private static BuildGrant NewGrant(int processId, int requestedNodes)
        => new(Guid.NewGuid(), processId, requestedNodes);

    [Fact]
    public void Constructor_BudgetLessThanOne_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new NodeBudgetManager(0));
    }

    [Fact]
    public void TryGrant_SingleBuild_GrantsUpToRequestedNodes()
    {
        NodeBudgetManager manager = new(totalBudget: 16);
        BuildGrant grant = NewGrant(processId: 1, requestedNodes: 16);

        int granted = manager.TryGrant(grant);

        granted.ShouldBe(16);
        grant.GrantedNodes.ShouldBe(16);
        manager.AllocatedNodes.ShouldBe(16);
        manager.ActiveBuildCount.ShouldBe(1);
    }

    [Fact]
    public void TryGrant_SingleBuild_RequestLessThanBudget_GrantsOnlyRequested()
    {
        NodeBudgetManager manager = new(totalBudget: 16);
        BuildGrant grant = NewGrant(processId: 1, requestedNodes: 4);

        int granted = manager.TryGrant(grant);

        granted.ShouldBe(4);
        manager.AvailableNodes.ShouldBe(12);
    }

    [Fact]
    public void TryGrant_BudgetExhausted_QueuesBuilds()
    {
        NodeBudgetManager manager = new(totalBudget: 4);
        BuildGrant grant1 = NewGrant(processId: 1, requestedNodes: 4);
        BuildGrant grant2 = NewGrant(processId: 2, requestedNodes: 4);

        manager.TryGrant(grant1).ShouldBe(4);

        int granted2 = manager.TryGrant(grant2);

        granted2.ShouldBe(0);
        grant2.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(1);
    }

    [Fact]
    public void TryGrant_FairShare_DividesBudgetAmongContenders()
    {
        // Budget of 16. First build takes 8. Second arrives with 1 waiting.
        // Wait queue has 1 entry, so contenders = 1 (waiter) + 1 (new arrival) = 2.
        // Available = 8, fairShare = 8/2 = 4.
        NodeBudgetManager manager = new(totalBudget: 16);
        BuildGrant grant1 = NewGrant(processId: 1, requestedNodes: 8);
        manager.TryGrant(grant1).ShouldBe(8);

        // Exhaust budget so grant2 queues.
        BuildGrant filler = NewGrant(processId: 99, requestedNodes: 8);
        manager.TryGrant(filler).ShouldBe(8);

        BuildGrant grant2 = NewGrant(processId: 2, requestedNodes: 16);
        manager.TryGrant(grant2).ShouldBe(0); // queued

        // Release filler to free 8 nodes. Now grant3 arrives with grant2 waiting.
        manager.Release(filler);

        // grant2 should have been drained from the queue.
        grant2.IsActive.ShouldBeTrue();
        grant2.GrantedNodes.ShouldBe(8);
    }

    [Fact]
    public void TryGrant_FairShare_WithWaitingBuilds_NewArrivalGetsShare()
    {
        // Budget of 8. Exhaust it, queue two builds, release all.
        // Then a new build arrives while two are waiting.
        NodeBudgetManager manager = new(totalBudget: 8);
        BuildGrant blocker = NewGrant(processId: 1, requestedNodes: 8);
        manager.TryGrant(blocker).ShouldBe(8);

        BuildGrant waiter1 = NewGrant(processId: 2, requestedNodes: 8);
        BuildGrant waiter2 = NewGrant(processId: 3, requestedNodes: 8);
        manager.TryGrant(waiter1).ShouldBe(0);
        manager.TryGrant(waiter2).ShouldBe(0);
        manager.WaitingBuildCount.ShouldBe(2);

        // Release blocker. DrainWaitQueue should give fair share to both waiters.
        // Available = 8, contenders = 2 → fairShare = 4 each.
        ImmutableArray<BuildGrant> newlyGranted = manager.Release(blocker);

        newlyGranted.Length.ShouldBe(2);
        waiter1.GrantedNodes.ShouldBe(4);
        waiter2.GrantedNodes.ShouldBe(4);
        manager.AllocatedNodes.ShouldBe(8);
    }

    [Fact]
    public void TryGrant_MinimumGrantIsOne()
    {
        // Budget of 1. One build active, one queued. Release active.
        // The queued build should get at least 1 node.
        NodeBudgetManager manager = new(totalBudget: 1);
        BuildGrant grant1 = NewGrant(processId: 1, requestedNodes: 1);
        manager.TryGrant(grant1).ShouldBe(1);

        BuildGrant grant2 = NewGrant(processId: 2, requestedNodes: 16);
        manager.TryGrant(grant2).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(grant1);
        newlyGranted.Length.ShouldBe(1);
        grant2.GrantedNodes.ShouldBe(1);
    }

    [Fact]
    public void Release_ActiveGrant_FreesNodes()
    {
        NodeBudgetManager manager = new(totalBudget: 16);
        BuildGrant grant = NewGrant(processId: 1, requestedNodes: 8);
        manager.TryGrant(grant);

        manager.Release(grant);

        manager.AllocatedNodes.ShouldBe(0);
        manager.ActiveBuildCount.ShouldBe(0);
        grant.GrantedNodes.ShouldBe(0);
    }

    [Fact]
    public void Release_WaitingGrant_RemovesFromQueue()
    {
        NodeBudgetManager manager = new(totalBudget: 4);
        BuildGrant active = NewGrant(processId: 1, requestedNodes: 4);
        manager.TryGrant(active);

        BuildGrant waiting = NewGrant(processId: 2, requestedNodes: 4);
        manager.TryGrant(waiting).ShouldBe(0);
        manager.WaitingBuildCount.ShouldBe(1);

        manager.Release(waiting);

        manager.WaitingBuildCount.ShouldBe(0);
        manager.AllocatedNodes.ShouldBe(4); // active grant unchanged
    }

    [Fact]
    public void Release_NestedGrant_DoesNotReturnRootBudget()
    {
        NodeBudgetManager manager = new(totalBudget: 4);
        BuildGrant root = NewGrant(processId: 1, requestedNodes: 4);
        manager.TryGrant(root).ShouldBe(4);

        BuildGrant nested = new(Guid.NewGuid(), processId: 2, requestedNodes: 4, root.GrantId, isNested: true)
        {
            GrantedNodes = 4,
        };

        BuildGrant waiting = NewGrant(processId: 3, requestedNodes: 4);
        manager.TryGrant(waiting).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(nested);

        newlyGranted.ShouldBeEmpty();
        manager.AllocatedNodes.ShouldBe(4);
        manager.ActiveBuildCount.ShouldBe(1);
        manager.WaitingBuildCount.ShouldBe(1);
    }

    [Fact]
    public void TryGrant_NestedGrant_DoesNotConsumeBudget()
    {
        NodeBudgetManager manager = new(totalBudget: 4);
        BuildGrant root = NewGrant(processId: 1, requestedNodes: 4);
        manager.TryGrant(root).ShouldBe(4);

        BuildGrant nested = new(Guid.NewGuid(), processId: 2, requestedNodes: 4, root.GrantId, isNested: true)
        {
            GrantedNodes = 4,
        };

        manager.TryGrant(nested).ShouldBe(4);

        manager.AllocatedNodes.ShouldBe(4);
        manager.ActiveBuildCount.ShouldBe(1);
        manager.AvailableNodes.ShouldBe(0);
    }

    [Fact]
    public void Release_DrainsWaitQueue_InFIFOOrder()
    {
        NodeBudgetManager manager = new(totalBudget: 8);
        BuildGrant blocker = NewGrant(processId: 1, requestedNodes: 8);
        manager.TryGrant(blocker);

        BuildGrant waiter1 = NewGrant(processId: 2, requestedNodes: 4);
        BuildGrant waiter2 = NewGrant(processId: 3, requestedNodes: 4);
        manager.TryGrant(waiter1);
        manager.TryGrant(waiter2);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(blocker);

        newlyGranted[0].ShouldBeSameAs(waiter1);
        newlyGranted[1].ShouldBeSameAs(waiter2);
    }

    [Fact]
    public void Release_DrainsWaitQueue_StopsWhenBudgetExhausted()
    {
        NodeBudgetManager manager = new(totalBudget: 2);
        BuildGrant blocker = NewGrant(processId: 1, requestedNodes: 2);
        manager.TryGrant(blocker);

        // Queue three builds that each want 1 node.
        BuildGrant waiter1 = NewGrant(processId: 2, requestedNodes: 1);
        BuildGrant waiter2 = NewGrant(processId: 3, requestedNodes: 1);
        BuildGrant waiter3 = NewGrant(processId: 4, requestedNodes: 1);
        manager.TryGrant(waiter1);
        manager.TryGrant(waiter2);
        manager.TryGrant(waiter3);

        // Release blocker frees 2 nodes. Fair share: 2/3 = 0 → min 1 each.
        // Two waiters get 1 node each, third stays queued.
        ImmutableArray<BuildGrant> newlyGranted = manager.Release(blocker);

        newlyGranted.Length.ShouldBe(2);
        manager.WaitingBuildCount.ShouldBe(1);
        manager.AllocatedNodes.ShouldBe(2);
    }

    [Fact]
    public void MultipleBuilds_FairShare_AccountingStaysConsistent()
    {
        NodeBudgetManager manager = new(totalBudget: 16);

        // Simulate several builds arriving and departing.
        BuildGrant g1 = NewGrant(processId: 1, requestedNodes: 16);
        BuildGrant g2 = NewGrant(processId: 2, requestedNodes: 16);
        BuildGrant g3 = NewGrant(processId: 3, requestedNodes: 16);

        manager.TryGrant(g1); // gets 16
        manager.TryGrant(g2); // queued
        manager.TryGrant(g3); // queued

        manager.Release(g1); // g2 and g3 drain

        // Both should be active, total allocated should not exceed budget.
        manager.AllocatedNodes.ShouldBeGreaterThan(0);
        manager.AllocatedNodes.ShouldBeLessThanOrEqualTo(16);
        manager.ActiveBuildCount.ShouldBe(2);
        manager.WaitingBuildCount.ShouldBe(0);

        // Release both.
        manager.Release(g2);
        manager.Release(g3);

        manager.AllocatedNodes.ShouldBe(0);
        manager.ActiveBuildCount.ShouldBe(0);
    }
}
