// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.Build.Framework.Coordinator;
using Shouldly;
using Xunit;

namespace Microsoft.Build.Coordinator.UnitTests;

public class NodeBudgetManager_Tests
{
    private static BuildGrant NewGrant(int processId, int requestedNodes, CoordinatorBuildPriority priority = CoordinatorBuildPriority.Normal)
        => new(Guid.NewGuid(), processId, requestedNodes, priority);

    private static void AssertSingleGrant(ImmutableArray<BuildGrant> grants, BuildGrant expected)
    {
        grants.Length.ShouldBe(1);
        grants[0].ShouldBeSameAs(expected);
    }

    [Fact]
    public void Constructor_BudgetLessThanOne_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new NodeBudgetManager(0));
    }

    [Fact]
    public void Constructor_NegativeReservedNodes_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new NodeBudgetManager(totalBudget: 4, highPriorityReservedNodes: -1));
    }

    [Fact]
    public void Constructor_NegativeMaxNodesPerBuild_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new NodeBudgetManager(totalBudget: 4, maxNodesPerBuild: -1));
    }

    [Fact]
    public void Constructor_NonPositivePriorityAgingThreshold_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new NodeBudgetManager(totalBudget: 4, priorityAgingThreshold: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => new NodeBudgetManager(totalBudget: 4, priorityAgingThreshold: -1));
    }

    [Fact]
    public void Constructor_ReservedNodesClampedToLeaveOneNormalNode()
    {
        NodeBudgetManager manager = new(totalBudget: 4, highPriorityReservedNodes: 10);

        manager.HighPriorityReservedNodes.ShouldBe(3);
    }

    [Fact]
    public void Constructor_MaxNodesPerBuildClampedToBudget()
    {
        NodeBudgetManager manager = new(totalBudget: 4, maxNodesPerBuild: 10);

        manager.MaxNodesPerBuild.ShouldBe(4);
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
    public void TryGrant_MaxNodesPerBuild_CapsSingleBuildGrant()
    {
        NodeBudgetManager manager = new(totalBudget: 16, maxNodesPerBuild: 4);
        BuildGrant grant = NewGrant(processId: 1, requestedNodes: 16);

        int granted = manager.TryGrant(grant);

        granted.ShouldBe(4);
        grant.GrantedNodes.ShouldBe(4);
        manager.AllocatedNodes.ShouldBe(4);
        manager.AvailableNodes.ShouldBe(12);
    }

    [Fact]
    public void TryGrant_MaxNodesPerBuild_GrantsSmallRequest()
    {
        NodeBudgetManager manager = new(totalBudget: 16, maxNodesPerBuild: 4);
        BuildGrant grant = NewGrant(processId: 1, requestedNodes: 2);

        int granted = manager.TryGrant(grant);

        granted.ShouldBe(2);
        grant.GrantedNodes.ShouldBe(2);
    }

    [Fact]
    public void TryGrant_MaxNodesPerBuild_QueuesLargeRequestWhenOnlyPartialSliceAvailable()
    {
        NodeBudgetManager manager = new(totalBudget: 10, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant normal1 = NewGrant(processId: 1, requestedNodes: 16);
        BuildGrant normal2 = NewGrant(processId: 2, requestedNodes: 16);

        manager.TryGrant(normal1).ShouldBe(4);

        int granted = manager.TryGrant(normal2);

        granted.ShouldBe(0);
        normal2.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(1);
        manager.AvailableNodes.ShouldBe(6);
    }

    [Fact]
    public void TryGrant_HighPriorityCanUseCapacityRoundedOutOfNormalPool()
    {
        NodeBudgetManager manager = new(totalBudget: 10, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant normal1 = NewGrant(processId: 1, requestedNodes: 16);
        BuildGrant normal2 = NewGrant(processId: 2, requestedNodes: 16);
        manager.TryGrant(normal1).ShouldBe(4);
        manager.TryGrant(normal2).ShouldBe(0);

        BuildGrant high = NewGrant(processId: 3, requestedNodes: 2, CoordinatorBuildPriority.High);

        int granted = manager.TryGrant(high);

        granted.ShouldBe(2);
        high.GrantedNodes.ShouldBe(2);
        normal2.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(1);
        manager.AllocatedNodes.ShouldBe(6);
    }

    [Fact]
    public void TryGrant_MaxNodesPerBuildGreaterThanNormalPool_GrantsNormalPool()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 5, maxNodesPerBuild: 4);
        BuildGrant normal = NewGrant(processId: 1, requestedNodes: 16);

        int granted = manager.TryGrant(normal);

        granted.ShouldBe(3);
        normal.GrantedNodes.ShouldBe(3);
        manager.AllocatedNodes.ShouldBe(3);
        manager.AvailableNodes.ShouldBe(5);
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
        NodeBudgetManager manager = new(totalBudget: 16);
        BuildGrant grant1 = NewGrant(processId: 1, requestedNodes: 8);
        manager.TryGrant(grant1).ShouldBe(8);

        BuildGrant filler = NewGrant(processId: 99, requestedNodes: 8);
        manager.TryGrant(filler).ShouldBe(8);

        BuildGrant grant2 = NewGrant(processId: 2, requestedNodes: 16);
        manager.TryGrant(grant2).ShouldBe(0);

        manager.Release(filler);

        grant2.IsActive.ShouldBeTrue();
        grant2.GrantedNodes.ShouldBe(8);
    }

    [Fact]
    public void TryGrant_FairShare_WithWaitingBuilds_NewArrivalGetsShare()
    {
        NodeBudgetManager manager = new(totalBudget: 8);
        BuildGrant blocker = NewGrant(processId: 1, requestedNodes: 8);
        manager.TryGrant(blocker).ShouldBe(8);

        BuildGrant waiter1 = NewGrant(processId: 2, requestedNodes: 8);
        BuildGrant waiter2 = NewGrant(processId: 3, requestedNodes: 8);
        manager.TryGrant(waiter1).ShouldBe(0);
        manager.TryGrant(waiter2).ShouldBe(0);
        manager.WaitingBuildCount.ShouldBe(2);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(blocker);

        newlyGranted.Length.ShouldBe(2);
        waiter1.GrantedNodes.ShouldBe(4);
        waiter2.GrantedNodes.ShouldBe(4);
        manager.AllocatedNodes.ShouldBe(8);
    }

    [Fact]
    public void Release_DrainsWaitQueue_AllNormalPreservesFifoFairShare()
    {
        NodeBudgetManager manager = new(totalBudget: 8);
        BuildGrant blocker = NewGrant(processId: 1, requestedNodes: 8);
        manager.TryGrant(blocker).ShouldBe(8);

        BuildGrant waiter1 = NewGrant(processId: 2, requestedNodes: 8);
        BuildGrant waiter2 = NewGrant(processId: 3, requestedNodes: 8);
        manager.TryGrant(waiter1).ShouldBe(0);
        manager.TryGrant(waiter2).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(blocker);

        newlyGranted.Length.ShouldBe(2);
        newlyGranted[0].ShouldBeSameAs(waiter1);
        newlyGranted[1].ShouldBeSameAs(waiter2);
        waiter1.GrantedNodes.ShouldBe(4);
        waiter2.GrantedNodes.ShouldBe(4);
        manager.AllocatedNodes.ShouldBe(8);
        manager.WaitingBuildCount.ShouldBe(0);
    }

    [Fact]
    public void Release_DrainsWaitQueue_HighPriorityBeforeNormal()
    {
        NodeBudgetManager manager = new(totalBudget: 4);
        BuildGrant blocker = NewGrant(processId: 1, requestedNodes: 4);
        manager.TryGrant(blocker).ShouldBe(4);

        BuildGrant normal = NewGrant(processId: 2, requestedNodes: 4);
        BuildGrant high = NewGrant(processId: 3, requestedNodes: 2, CoordinatorBuildPriority.High);
        manager.TryGrant(normal).ShouldBe(0);
        manager.TryGrant(high).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(blocker);

        newlyGranted.Length.ShouldBe(2);
        newlyGranted[0].ShouldBeSameAs(high);
        newlyGranted[1].ShouldBeSameAs(normal);
        high.GrantedNodes.ShouldBe(2);
        normal.GrantedNodes.ShouldBe(2);
        manager.WaitingBuildCount.ShouldBe(0);
    }

    [Fact]
    public void Release_DrainsWaitQueue_PreservesFifoWithinPriority()
    {
        NodeBudgetManager manager = new(totalBudget: 6);
        BuildGrant blocker = NewGrant(processId: 1, requestedNodes: 6);
        manager.TryGrant(blocker).ShouldBe(6);

        BuildGrant high1 = NewGrant(processId: 2, requestedNodes: 3, CoordinatorBuildPriority.High);
        BuildGrant normal = NewGrant(processId: 3, requestedNodes: 3);
        BuildGrant high2 = NewGrant(processId: 4, requestedNodes: 3, CoordinatorBuildPriority.High);
        manager.TryGrant(high1).ShouldBe(0);
        manager.TryGrant(normal).ShouldBe(0);
        manager.TryGrant(high2).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(blocker);

        newlyGranted.Length.ShouldBe(2);
        newlyGranted[0].ShouldBeSameAs(high1);
        newlyGranted[1].ShouldBeSameAs(high2);
        normal.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(1);
    }

    [Fact]
    public void Release_DrainsWaitQueue_AgesLowPriorityWaiterAfterBypasses()
    {
        NodeBudgetManager manager = new(totalBudget: 1);
        BuildGrant blocker = NewGrant(processId: 1, requestedNodes: 1);
        manager.TryGrant(blocker).ShouldBe(1);

        BuildGrant low = NewGrant(processId: 2, requestedNodes: 1, CoordinatorBuildPriority.Low);
        BuildGrant high1 = NewGrant(processId: 3, requestedNodes: 1, CoordinatorBuildPriority.High);
        BuildGrant high2 = NewGrant(processId: 4, requestedNodes: 1, CoordinatorBuildPriority.High);
        BuildGrant high3 = NewGrant(processId: 5, requestedNodes: 1, CoordinatorBuildPriority.High);
        BuildGrant high4 = NewGrant(processId: 6, requestedNodes: 1, CoordinatorBuildPriority.High);
        BuildGrant high5 = NewGrant(processId: 7, requestedNodes: 1, CoordinatorBuildPriority.High);
        BuildGrant high6 = NewGrant(processId: 8, requestedNodes: 1, CoordinatorBuildPriority.High);
        BuildGrant high7 = NewGrant(processId: 9, requestedNodes: 1, CoordinatorBuildPriority.High);

        manager.TryGrant(low).ShouldBe(0);
        manager.TryGrant(high1).ShouldBe(0);
        manager.TryGrant(high2).ShouldBe(0);
        manager.TryGrant(high3).ShouldBe(0);
        manager.TryGrant(high4).ShouldBe(0);
        manager.TryGrant(high5).ShouldBe(0);
        manager.TryGrant(high6).ShouldBe(0);
        manager.TryGrant(high7).ShouldBe(0);

        AssertSingleGrant(manager.Release(blocker), high1);
        AssertSingleGrant(manager.Release(high1), high2);
        AssertSingleGrant(manager.Release(high2), high3);
        AssertSingleGrant(manager.Release(high3), high4);
        AssertSingleGrant(manager.Release(high4), high5);
        AssertSingleGrant(manager.Release(high5), high6);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(high6);

        AssertSingleGrant(newlyGranted, low);
        low.BypassCount.ShouldBe(6);
        high7.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void TryGrant_NewNormalDoesNotBypassAgedLowPriorityWaiter()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant normalActive = NewGrant(processId: 1, requestedNodes: 2);
        manager.TryGrant(normalActive).ShouldBe(2);

        BuildGrant lowWaiting = NewGrant(processId: 2, requestedNodes: 4, CoordinatorBuildPriority.Low);
        manager.TryGrant(lowWaiting).ShouldBe(0);

        BuildGrant high1 = NewGrant(processId: 3, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high1).ShouldBe(4);
        manager.Release(high1).ShouldBeEmpty();
        BuildGrant high2 = NewGrant(processId: 4, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high2).ShouldBe(4);
        manager.Release(high2).ShouldBeEmpty();
        BuildGrant high3 = NewGrant(processId: 5, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high3).ShouldBe(4);
        manager.Release(high3).ShouldBeEmpty();

        lowWaiting.BypassCount.ShouldBe(3);

        BuildGrant normalWaiting = NewGrant(processId: 6, requestedNodes: 1);
        manager.TryGrant(normalWaiting).ShouldBe(0);

        lowWaiting.IsActive.ShouldBeFalse();
        normalWaiting.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(2);
        manager.AvailableNodes.ShouldBe(6);
    }

    [Fact]
    public void TryGrant_CustomPriorityAgingThresholdControlsPromotionRate()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 4, maxNodesPerBuild: 4, priorityAgingThreshold: 2);
        BuildGrant normalActive = NewGrant(processId: 1, requestedNodes: 2);
        manager.TryGrant(normalActive).ShouldBe(2);

        BuildGrant lowWaiting = NewGrant(processId: 2, requestedNodes: 4, CoordinatorBuildPriority.Low);
        manager.TryGrant(lowWaiting).ShouldBe(0);

        BuildGrant high1 = NewGrant(processId: 3, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high1).ShouldBe(4);
        manager.Release(high1).ShouldBeEmpty();
        BuildGrant high2 = NewGrant(processId: 4, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high2).ShouldBe(4);
        manager.Release(high2).ShouldBeEmpty();

        lowWaiting.BypassCount.ShouldBe(2);

        BuildGrant normalWaiting = NewGrant(processId: 5, requestedNodes: 1);
        manager.TryGrant(normalWaiting).ShouldBe(0);

        lowWaiting.IsActive.ShouldBeFalse();
        normalWaiting.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(2);
        manager.AvailableNodes.ShouldBe(6);
    }

    [Fact]
    public void TryGrant_NewHighDoesNotBypassAgedWaiterWhenNoReservedCapacity()
    {
        NodeBudgetManager manager = new(totalBudget: 8, maxNodesPerBuild: 4);
        BuildGrant normalActive1 = NewGrant(processId: 1, requestedNodes: 4);
        BuildGrant normalActive2 = NewGrant(processId: 2, requestedNodes: 2);
        manager.TryGrant(normalActive1).ShouldBe(4);
        manager.TryGrant(normalActive2).ShouldBe(2);

        BuildGrant normalWaiting = NewGrant(processId: 3, requestedNodes: 4);
        manager.TryGrant(normalWaiting).ShouldBe(0);

        BuildGrant high1 = NewGrant(processId: 4, requestedNodes: 2, CoordinatorBuildPriority.High);
        manager.TryGrant(high1).ShouldBe(2);
        manager.Release(high1).ShouldBeEmpty();
        BuildGrant high2 = NewGrant(processId: 5, requestedNodes: 2, CoordinatorBuildPriority.High);
        manager.TryGrant(high2).ShouldBe(2);
        manager.Release(high2).ShouldBeEmpty();
        BuildGrant high3 = NewGrant(processId: 6, requestedNodes: 2, CoordinatorBuildPriority.High);
        manager.TryGrant(high3).ShouldBe(2);
        manager.Release(high3).ShouldBeEmpty();

        normalWaiting.BypassCount.ShouldBe(3);

        BuildGrant newHigh = NewGrant(processId: 7, requestedNodes: 2, CoordinatorBuildPriority.High);
        manager.TryGrant(newHigh).ShouldBe(0);

        normalWaiting.IsActive.ShouldBeFalse();
        newHigh.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(2);
        manager.AvailableNodes.ShouldBe(2);
    }

    [Fact]
    public void Release_DoesNotFallbackToRawHighBehindAgedWaiterWithoutReservedCapacity()
    {
        NodeBudgetManager manager = new(totalBudget: 8, maxNodesPerBuild: 4);
        BuildGrant normalActive1 = NewGrant(processId: 1, requestedNodes: 4);
        BuildGrant normalActive2 = NewGrant(processId: 2, requestedNodes: 2);
        manager.TryGrant(normalActive1).ShouldBe(4);
        manager.TryGrant(normalActive2).ShouldBe(2);

        BuildGrant normalWaiting = NewGrant(processId: 3, requestedNodes: 4);
        manager.TryGrant(normalWaiting).ShouldBe(0);

        BuildGrant high1 = NewGrant(processId: 4, requestedNodes: 2, CoordinatorBuildPriority.High);
        manager.TryGrant(high1).ShouldBe(2);
        manager.Release(high1).ShouldBeEmpty();
        BuildGrant high2 = NewGrant(processId: 5, requestedNodes: 2, CoordinatorBuildPriority.High);
        manager.TryGrant(high2).ShouldBe(2);
        manager.Release(high2).ShouldBeEmpty();
        BuildGrant high3 = NewGrant(processId: 6, requestedNodes: 2, CoordinatorBuildPriority.High);
        manager.TryGrant(high3).ShouldBe(2);
        manager.Release(high3).ShouldBeEmpty();

        normalWaiting.BypassCount.ShouldBe(3);

        BuildGrant highWaiting = NewGrant(processId: 7, requestedNodes: 2, CoordinatorBuildPriority.High);
        BuildGrant cancelledWaiter = NewGrant(processId: 8, requestedNodes: 1);
        manager.TryGrant(highWaiting).ShouldBe(0);
        manager.TryGrant(cancelledWaiter).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(cancelledWaiter);

        newlyGranted.ShouldBeEmpty();
        normalWaiting.BypassCount.ShouldBe(3);
        normalWaiting.IsActive.ShouldBeFalse();
        highWaiting.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(2);
        manager.AvailableNodes.ShouldBe(2);
    }

    [Fact]
    public void TryGrant_HighPriorityReserve_WithholdsNodesFromNormalGrant()
    {
        NodeBudgetManager manager = new(totalBudget: 4, highPriorityReservedNodes: 1);
        BuildGrant normal = NewGrant(processId: 1, requestedNodes: 4);

        int granted = manager.TryGrant(normal);

        granted.ShouldBe(3);
        normal.GrantedNodes.ShouldBe(3);
        manager.AllocatedNodes.ShouldBe(3);
        manager.AvailableNodes.ShouldBe(1);
    }

    [Fact]
    public void TryGrant_HighPriorityReserve_AllowsHighPriorityToUseReservedNode()
    {
        NodeBudgetManager manager = new(totalBudget: 4, highPriorityReservedNodes: 1);
        BuildGrant normal = NewGrant(processId: 1, requestedNodes: 4);
        manager.TryGrant(normal).ShouldBe(3);

        BuildGrant high = NewGrant(processId: 2, requestedNodes: 4, CoordinatorBuildPriority.High);

        int granted = manager.TryGrant(high);

        granted.ShouldBe(1);
        high.GrantedNodes.ShouldBe(1);
        manager.AllocatedNodes.ShouldBe(4);
    }

    [Fact]
    public void TryGrant_HighPriorityReserveWithMaxNodesPerBuild_AllowsHighPriorityToUseReservedSlice()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant normal = NewGrant(processId: 1, requestedNodes: 16);
        manager.TryGrant(normal).ShouldBe(4);

        BuildGrant high = NewGrant(processId: 2, requestedNodes: 16, CoordinatorBuildPriority.High);

        int granted = manager.TryGrant(high);

        granted.ShouldBe(4);
        high.GrantedNodes.ShouldBe(4);
        manager.AllocatedNodes.ShouldBe(8);
    }

    [Fact]
    public void TryGrant_MaxNodesPerBuild_CapsHighPriorityWhenBudgetIsFree()
    {
        NodeBudgetManager manager = new(totalBudget: 16, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant high = NewGrant(processId: 1, requestedNodes: 16, CoordinatorBuildPriority.High);

        int granted = manager.TryGrant(high);

        granted.ShouldBe(4);
        high.GrantedNodes.ShouldBe(4);
        manager.AllocatedNodes.ShouldBe(4);
    }

    [Fact]
    public void TryGrant_HighPriorityStartedFirst_DoesNotConsumeNormalSliceBudget()
    {
        NodeBudgetManager manager = new(totalBudget: 16, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant high = NewGrant(processId: 1, requestedNodes: 16, CoordinatorBuildPriority.High);
        manager.TryGrant(high).ShouldBe(4);

        BuildGrant normal1 = NewGrant(processId: 2, requestedNodes: 16);
        BuildGrant normal2 = NewGrant(processId: 3, requestedNodes: 16);
        BuildGrant normal3 = NewGrant(processId: 4, requestedNodes: 16);

        manager.TryGrant(normal1).ShouldBe(4);
        manager.TryGrant(normal2).ShouldBe(4);
        manager.TryGrant(normal3).ShouldBe(4);

        manager.AllocatedNodes.ShouldBe(16);
        manager.WaitingBuildCount.ShouldBe(0);
    }

    [Fact]
    public void Release_Normal_DrainsNormalWaiterWhileHighPriorityBuildStaysActive()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant normal1 = NewGrant(processId: 1, requestedNodes: 4);
        BuildGrant high = NewGrant(processId: 2, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(normal1).ShouldBe(4);
        manager.TryGrant(high).ShouldBe(4);

        BuildGrant normal2 = NewGrant(processId: 3, requestedNodes: 4);
        manager.TryGrant(normal2).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(normal1);

        AssertSingleGrant(newlyGranted, normal2);
        normal2.GrantedNodes.ShouldBe(4);
        high.IsActive.ShouldBeTrue();
        manager.AllocatedNodes.ShouldBe(8);
        manager.WaitingBuildCount.ShouldBe(0);
    }

    [Fact]
    public void TryGrant_NewNormalDoesNotBypassQueuedHighPriorityWaitingForFullSlice()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant highActive = NewGrant(processId: 1, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(highActive).ShouldBe(4);

        BuildGrant normal1 = NewGrant(processId: 2, requestedNodes: 1);
        BuildGrant normal2 = NewGrant(processId: 3, requestedNodes: 1);
        BuildGrant normal3 = NewGrant(processId: 4, requestedNodes: 1);
        BuildGrant normal4 = NewGrant(processId: 5, requestedNodes: 1);
        manager.TryGrant(normal1).ShouldBe(1);
        manager.TryGrant(normal2).ShouldBe(1);
        manager.TryGrant(normal3).ShouldBe(1);
        manager.TryGrant(normal4).ShouldBe(1);

        BuildGrant highWaiting = NewGrant(processId: 6, requestedNodes: 8, CoordinatorBuildPriority.High);
        manager.TryGrant(highWaiting).ShouldBe(0);

        manager.Release(normal1).ShouldBeEmpty();

        BuildGrant newNormal = NewGrant(processId: 7, requestedNodes: 1);
        manager.TryGrant(newNormal).ShouldBe(0);

        highWaiting.IsActive.ShouldBeFalse();
        newNormal.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(2);
        manager.AllocatedNodes.ShouldBe(7);
    }

    [Fact]
    public void TryGrant_NewHighPriorityBypassesAgedNormalWhenReservedCapacityIsAvailable()
    {
        NodeBudgetManager manager = new(totalBudget: 16, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant normal1 = NewGrant(processId: 1, requestedNodes: 4);
        BuildGrant normal2 = NewGrant(processId: 2, requestedNodes: 4);
        BuildGrant normal3 = NewGrant(processId: 3, requestedNodes: 4);
        manager.TryGrant(normal1).ShouldBe(4);
        manager.TryGrant(normal2).ShouldBe(4);
        manager.TryGrant(normal3).ShouldBe(4);

        BuildGrant normalWaiting = NewGrant(processId: 4, requestedNodes: 16);
        manager.TryGrant(normalWaiting).ShouldBe(0);

        BuildGrant high1 = NewGrant(processId: 5, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high1).ShouldBe(4);
        manager.Release(high1).ShouldBeEmpty();
        BuildGrant high2 = NewGrant(processId: 6, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high2).ShouldBe(4);
        manager.Release(high2).ShouldBeEmpty();
        BuildGrant high3 = NewGrant(processId: 7, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high3).ShouldBe(4);
        manager.Release(high3).ShouldBeEmpty();

        normalWaiting.BypassCount.ShouldBe(3);

        BuildGrant high4 = NewGrant(processId: 8, requestedNodes: 4, CoordinatorBuildPriority.High);

        manager.TryGrant(high4).ShouldBe(4);

        high4.IsActive.ShouldBeTrue();
        normalWaiting.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(1);
    }

    [Fact]
    public void Release_DrainsQueuedHighPriorityBehindAgedNormalWhenOnlyReservedCapacityIsAvailable()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant normalActive = NewGrant(processId: 1, requestedNodes: 4);
        BuildGrant highActive = NewGrant(processId: 2, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(normalActive).ShouldBe(4);
        manager.TryGrant(highActive).ShouldBe(4);

        BuildGrant normalWaiting = NewGrant(processId: 3, requestedNodes: 16);
        manager.TryGrant(normalWaiting).ShouldBe(0);

        manager.Release(highActive).ShouldBeEmpty();
        BuildGrant high1 = NewGrant(processId: 4, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high1).ShouldBe(4);
        manager.Release(high1).ShouldBeEmpty();
        BuildGrant high2 = NewGrant(processId: 5, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high2).ShouldBe(4);
        manager.Release(high2).ShouldBeEmpty();
        BuildGrant high3 = NewGrant(processId: 6, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high3).ShouldBe(4);

        normalWaiting.BypassCount.ShouldBe(3);

        BuildGrant highWaiting = NewGrant(processId: 7, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(highWaiting).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(high3);

        AssertSingleGrant(newlyGranted, highWaiting);
        highWaiting.GrantedNodes.ShouldBe(4);
        normalWaiting.BypassCount.ShouldBe(4);
        normalWaiting.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(1);
        manager.AvailableNodes.ShouldBe(0);
    }

    [Fact]
    public void Release_UncappedReservedFallback_DoesNotDiluteRawHighGrantAgainstAgedNormal()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 4);
        BuildGrant normalActive = NewGrant(processId: 1, requestedNodes: 4);
        manager.TryGrant(normalActive).ShouldBe(4);

        BuildGrant normalWaiting = NewGrant(processId: 2, requestedNodes: 8);
        manager.TryGrant(normalWaiting).ShouldBe(0);

        BuildGrant high1 = NewGrant(processId: 3, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high1).ShouldBe(4);
        manager.Release(high1).ShouldBeEmpty();
        BuildGrant high2 = NewGrant(processId: 4, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high2).ShouldBe(4);
        manager.Release(high2).ShouldBeEmpty();
        BuildGrant highActive = NewGrant(processId: 5, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(highActive).ShouldBe(4);

        normalWaiting.BypassCount.ShouldBe(3);

        BuildGrant highWaiting = NewGrant(processId: 6, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(highWaiting).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(highActive);

        AssertSingleGrant(newlyGranted, highWaiting);
        highWaiting.GrantedNodes.ShouldBe(4);
        normalWaiting.BypassCount.ShouldBe(4);
        normalWaiting.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(1);
        manager.AvailableNodes.ShouldBe(0);
    }

    [Fact]
    public void Release_UncappedReservedFallback_SplitsGrantAcrossRawHighWaiters()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 4);
        BuildGrant normalActive = NewGrant(processId: 1, requestedNodes: 4);
        manager.TryGrant(normalActive).ShouldBe(4);

        BuildGrant normalWaiting = NewGrant(processId: 2, requestedNodes: 8);
        manager.TryGrant(normalWaiting).ShouldBe(0);

        BuildGrant high1 = NewGrant(processId: 3, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high1).ShouldBe(4);
        manager.Release(high1).ShouldBeEmpty();
        BuildGrant high2 = NewGrant(processId: 4, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(high2).ShouldBe(4);
        manager.Release(high2).ShouldBeEmpty();
        BuildGrant highActive = NewGrant(processId: 5, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(highActive).ShouldBe(4);

        normalWaiting.BypassCount.ShouldBe(3);

        BuildGrant highWaiting1 = NewGrant(processId: 6, requestedNodes: 4, CoordinatorBuildPriority.High);
        BuildGrant highWaiting2 = NewGrant(processId: 7, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(highWaiting1).ShouldBe(0);
        manager.TryGrant(highWaiting2).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(highActive);

        newlyGranted.Length.ShouldBe(2);
        newlyGranted[0].ShouldBeSameAs(highWaiting1);
        newlyGranted[1].ShouldBeSameAs(highWaiting2);
        highWaiting1.GrantedNodes.ShouldBe(2);
        highWaiting2.GrantedNodes.ShouldBe(2);
        normalWaiting.BypassCount.ShouldBe(5);
        normalWaiting.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(1);
        manager.AvailableNodes.ShouldBe(0);
    }

    [Fact]
    public void Release_DoesNotBypassOlderHighPriorityWaiterForLaterSmallHighPriorityWaiter()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant normalActive = NewGrant(processId: 1, requestedNodes: 4);
        BuildGrant highActive = NewGrant(processId: 2, requestedNodes: 2, CoordinatorBuildPriority.High);
        manager.TryGrant(normalActive).ShouldBe(4);
        manager.TryGrant(highActive).ShouldBe(2);

        BuildGrant normalWaiting = NewGrant(processId: 3, requestedNodes: 16);
        manager.TryGrant(normalWaiting).ShouldBe(0);

        BuildGrant high1 = NewGrant(processId: 4, requestedNodes: 1, CoordinatorBuildPriority.High);
        manager.TryGrant(high1).ShouldBe(1);
        manager.Release(high1).ShouldBeEmpty();
        BuildGrant high2 = NewGrant(processId: 5, requestedNodes: 1, CoordinatorBuildPriority.High);
        manager.TryGrant(high2).ShouldBe(1);
        manager.Release(high2).ShouldBeEmpty();
        BuildGrant high3 = NewGrant(processId: 6, requestedNodes: 1, CoordinatorBuildPriority.High);
        manager.TryGrant(high3).ShouldBe(1);
        manager.Release(high3).ShouldBeEmpty();

        normalWaiting.BypassCount.ShouldBe(3);

        BuildGrant highWaitingLarge = NewGrant(processId: 7, requestedNodes: 4, CoordinatorBuildPriority.High);
        BuildGrant highWaitingSmall = NewGrant(processId: 8, requestedNodes: 2, CoordinatorBuildPriority.High);
        BuildGrant cancelledWaiter = NewGrant(processId: 9, requestedNodes: 1);
        manager.TryGrant(highWaitingLarge).ShouldBe(0);
        manager.TryGrant(highWaitingSmall).ShouldBe(0);
        manager.TryGrant(cancelledWaiter).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(cancelledWaiter);

        newlyGranted.ShouldBeEmpty();
        normalWaiting.BypassCount.ShouldBe(3);
        highWaitingLarge.IsActive.ShouldBeFalse();
        highWaitingSmall.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(3);
        manager.AvailableNodes.ShouldBe(2);
    }

    [Fact]
    public void TryGrant_NewHigherPriorityBuildMayBypassQueuedLowerPriorityWaiter()
    {
        NodeBudgetManager manager = new(totalBudget: 8, highPriorityReservedNodes: 4, maxNodesPerBuild: 4);
        BuildGrant highActive = NewGrant(processId: 1, requestedNodes: 4, CoordinatorBuildPriority.High);
        manager.TryGrant(highActive).ShouldBe(4);

        BuildGrant normal1 = NewGrant(processId: 2, requestedNodes: 1);
        BuildGrant normal2 = NewGrant(processId: 3, requestedNodes: 1);
        BuildGrant normal3 = NewGrant(processId: 4, requestedNodes: 1);
        BuildGrant normal4 = NewGrant(processId: 5, requestedNodes: 1);
        manager.TryGrant(normal1).ShouldBe(1);
        manager.TryGrant(normal2).ShouldBe(1);
        manager.TryGrant(normal3).ShouldBe(1);
        manager.TryGrant(normal4).ShouldBe(1);

        BuildGrant normalWaiting = NewGrant(processId: 6, requestedNodes: 8);
        manager.TryGrant(normalWaiting).ShouldBe(0);

        manager.Release(normal1).ShouldBeEmpty();

        BuildGrant newHigh = NewGrant(processId: 7, requestedNodes: 1, CoordinatorBuildPriority.High);
        manager.TryGrant(newHigh).ShouldBe(1);

        normalWaiting.IsActive.ShouldBeFalse();
        normalWaiting.BypassCount.ShouldBe(1);
        newHigh.IsActive.ShouldBeTrue();
        manager.WaitingBuildCount.ShouldBe(1);
        manager.AllocatedNodes.ShouldBe(8);
    }

    [Fact]
    public void TryGrant_HighPriorityReserve_DoesNotCapHighPriorityWhenBudgetIsFree()
    {
        NodeBudgetManager manager = new(totalBudget: 4, highPriorityReservedNodes: 1);
        BuildGrant high = NewGrant(processId: 1, requestedNodes: 4, CoordinatorBuildPriority.High);

        int granted = manager.TryGrant(high);

        granted.ShouldBe(4);
        high.GrantedNodes.ShouldBe(4);
        manager.AllocatedNodes.ShouldBe(4);
    }

    [Fact]
    public void TryGrant_HighPriorityReserve_LeavesAtLeastOneNodeForNormalPriority()
    {
        NodeBudgetManager manager = new(totalBudget: 4, highPriorityReservedNodes: 10);
        BuildGrant normal = NewGrant(processId: 1, requestedNodes: 4);

        int granted = manager.TryGrant(normal);

        granted.ShouldBe(1);
        normal.GrantedNodes.ShouldBe(1);
        manager.AvailableNodes.ShouldBe(3);
    }

    [Fact]
    public void Release_MaxNodesPerBuild_DrainsWaitQueueInFullSlices()
    {
        NodeBudgetManager manager = new(totalBudget: 12, maxNodesPerBuild: 4);
        BuildGrant blocker = NewGrant(processId: 1, requestedNodes: 12);
        manager.TryGrant(blocker).ShouldBe(4);

        BuildGrant filler1 = NewGrant(processId: 2, requestedNodes: 4);
        BuildGrant filler2 = NewGrant(processId: 3, requestedNodes: 4);
        manager.TryGrant(filler1).ShouldBe(4);
        manager.TryGrant(filler2).ShouldBe(4);

        BuildGrant waiter1 = NewGrant(processId: 4, requestedNodes: 16);
        BuildGrant waiter2 = NewGrant(processId: 5, requestedNodes: 16);
        manager.TryGrant(waiter1).ShouldBe(0);
        manager.TryGrant(waiter2).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(blocker);

        newlyGranted.Length.ShouldBe(1);
        newlyGranted[0].ShouldBeSameAs(waiter1);
        waiter1.GrantedNodes.ShouldBe(4);
        waiter2.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(1);
    }

    [Fact]
    public void Release_MaxNodesPerBuild_DoesNotBypassOlderSamePriorityWaiterForPartialSlice()
    {
        NodeBudgetManager manager = new(totalBudget: 9, maxNodesPerBuild: 4);
        BuildGrant normal1 = NewGrant(processId: 1, requestedNodes: 2);
        BuildGrant normal2 = NewGrant(processId: 2, requestedNodes: 4);
        BuildGrant normal3 = NewGrant(processId: 3, requestedNodes: 2);
        manager.TryGrant(normal1).ShouldBe(2);
        manager.TryGrant(normal2).ShouldBe(4);
        manager.TryGrant(normal3).ShouldBe(2);

        BuildGrant waiter1 = NewGrant(processId: 4, requestedNodes: 4);
        BuildGrant waiter2 = NewGrant(processId: 5, requestedNodes: 1);
        manager.TryGrant(waiter1).ShouldBe(0);
        manager.TryGrant(waiter2).ShouldBe(0);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(normal3);

        newlyGranted.ShouldBeEmpty();
        waiter1.IsActive.ShouldBeFalse();
        waiter2.IsActive.ShouldBeFalse();
        manager.WaitingBuildCount.ShouldBe(2);
        manager.AllocatedNodes.ShouldBe(6);
        manager.AvailableNodes.ShouldBe(3);
    }

    [Fact]
    public void TryGrant_MinimumGrantIsOne()
    {
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
        manager.AllocatedNodes.ShouldBe(4);
    }

    [Fact]
    public void Release_NestedGrant_DoesNotReturnRootBudget()
    {
        NodeBudgetManager manager = new(totalBudget: 4);
        BuildGrant root = NewGrant(processId: 1, requestedNodes: 4);
        manager.TryGrant(root).ShouldBe(4);

        BuildGrant nested = new(Guid.NewGuid(), processId: 2, requestedNodes: 4, grantId: root.GrantId, isNested: true);
        nested.Activate(4);

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

        BuildGrant nested = new(Guid.NewGuid(), processId: 2, requestedNodes: 4, grantId: root.GrantId, isNested: true);
        nested.Activate(4);

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

        BuildGrant waiter1 = NewGrant(processId: 2, requestedNodes: 1);
        BuildGrant waiter2 = NewGrant(processId: 3, requestedNodes: 1);
        BuildGrant waiter3 = NewGrant(processId: 4, requestedNodes: 1);
        manager.TryGrant(waiter1);
        manager.TryGrant(waiter2);
        manager.TryGrant(waiter3);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(blocker);

        newlyGranted.Length.ShouldBe(2);
        manager.WaitingBuildCount.ShouldBe(1);
        manager.AllocatedNodes.ShouldBe(2);
    }

    [Fact]
    public void MultipleBuilds_FairShare_AccountingStaysConsistent()
    {
        NodeBudgetManager manager = new(totalBudget: 16);

        BuildGrant g1 = NewGrant(processId: 1, requestedNodes: 16);
        BuildGrant g2 = NewGrant(processId: 2, requestedNodes: 16);
        BuildGrant g3 = NewGrant(processId: 3, requestedNodes: 16);

        manager.TryGrant(g1);
        manager.TryGrant(g2);
        manager.TryGrant(g3);

        ImmutableArray<BuildGrant> newlyGranted = manager.Release(g1);

        newlyGranted.Length.ShouldBe(2);
        newlyGranted[0].ShouldBeSameAs(g2);
        newlyGranted[1].ShouldBeSameAs(g3);
        g2.GrantedNodes.ShouldBe(8);
        g3.GrantedNodes.ShouldBe(8);
        manager.AllocatedNodes.ShouldBe(16);
        manager.ActiveBuildCount.ShouldBe(2);
        manager.WaitingBuildCount.ShouldBe(0);

        manager.Release(g2);
        manager.Release(g3);

        manager.AllocatedNodes.ShouldBe(0);
        manager.ActiveBuildCount.ShouldBe(0);
    }
}
