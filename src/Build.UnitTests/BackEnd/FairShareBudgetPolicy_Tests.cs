// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET

using System;
using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class FairShareBudgetPolicy_Tests
    {
        [Fact]
        public void AutoDetect_Uses90PercentOfCores()
        {
            var policy = new FairShareBudgetPolicy();
            int expectedBudget = Math.Max(1, (int)(Environment.ProcessorCount * 0.9));

            policy.TotalBudget.ShouldBe(expectedBudget);
            policy.MaxConcurrentBuilds.ShouldBe(Math.Max(1, expectedBudget / 4));
        }

        [Fact]
        public void BudgetOnly_DerivesMaxBuilds()
        {
            var policy = new FairShareBudgetPolicy(12);

            policy.TotalBudget.ShouldBe(12);
            policy.MaxConcurrentBuilds.ShouldBe(3); // 12/4
        }

        [Fact]
        public void ExplicitBudgetAndMax()
        {
            var policy = new FairShareBudgetPolicy(12, 3);

            policy.TotalBudget.ShouldBe(12);
            policy.MaxConcurrentBuilds.ShouldBe(3);
        }

        [Fact]
        public void SingleBuild_NoQueue_GetsFairShare()
        {
            var policy = new FairShareBudgetPolicy(12, 3);

            // 1 active, 0 queued, 0 alloc → remaining=12, slots=3, fair=4
            int granted = policy.GetGrantedNodes(requestedNodes: 6, activeCount: 1, queuedCount: 0, allocatedNodes: 0);
            granted.ShouldBe(4); // min(4, 6) = 4
        }

        [Fact]
        public void SingleBuild_NoQueue_CappedAtRequested()
        {
            var policy = new FairShareBudgetPolicy(12, 3);

            int granted = policy.GetGrantedNodes(requestedNodes: 2, activeCount: 1, queuedCount: 0, allocatedNodes: 0);
            granted.ShouldBe(2); // min(4, 2) = 2
        }

        [Fact]
        public void TwoBuilds_NoQueue_SplitEvenly()
        {
            var policy = new FairShareBudgetPolicy(12, 3);

            // 2 active (1 already granted 4), 0 queued → remaining=8, slots=2, fair=4
            int granted = policy.GetGrantedNodes(requestedNodes: 12, activeCount: 2, queuedCount: 0, allocatedNodes: 4);
            granted.ShouldBe(4);
        }

        [Fact]
        public void WithQueue_DividesByMaxConcurrent()
        {
            var policy = new FairShareBudgetPolicy(12, 3);

            // 1 active, 2 queued, 0 alloc → remaining=12, slots=3, fair=4
            int granted = policy.GetGrantedNodes(requestedNodes: 6, activeCount: 1, queuedCount: 2, allocatedNodes: 0);
            granted.ShouldBe(4);
        }

        [Fact]
        public void WithQueue_CappedAtRequested()
        {
            var policy = new FairShareBudgetPolicy(12, 3);

            int granted = policy.GetGrantedNodes(requestedNodes: 2, activeCount: 1, queuedCount: 2, allocatedNodes: 0);
            granted.ShouldBe(2); // min(4, 2) = 2
        }

        [Fact]
        public void ThreeActiveBuilds_NoQueue()
        {
            var policy = new FairShareBudgetPolicy(12, 3);

            // 3 active (2 already granted 4 each), 0 queued → remaining=4, slots=1, fair=4
            int granted = policy.GetGrantedNodes(requestedNodes: 6, activeCount: 3, queuedCount: 0, allocatedNodes: 8);
            granted.ShouldBe(4);
        }

        [Fact]
        public void SmallBudget_FloorIsOne()
        {
            var policy = new FairShareBudgetPolicy(2, 4);

            // remaining=2, slots=4, fairShare = max(1, 2/4) = 1
            int granted = policy.GetGrantedNodes(requestedNodes: 6, activeCount: 1, queuedCount: 3, allocatedNodes: 0);
            granted.ShouldBe(1);
        }

        [Fact]
        public void RapidArrival_NeverExceedsBudget()
        {
            var policy = new FairShareBudgetPolicy(24, 4);
            int totalGranted = 0;

            for (int i = 0; i < 4; i++)
            {
                int granted = policy.GetGrantedNodes(requestedNodes: 12, activeCount: i + 1, queuedCount: 0, allocatedNodes: totalGranted);
                granted.ShouldBe(6); // 24/4 = 6 for every build
                totalGranted += granted;
            }

            totalGranted.ShouldBe(24);
        }

        [Fact]
        public void ZeroBudget_ClampsToOne()
        {
            var policy = new FairShareBudgetPolicy(0, 0);

            policy.TotalBudget.ShouldBe(1);
            policy.MaxConcurrentBuilds.ShouldBe(1);
        }
    }
}

#endif
