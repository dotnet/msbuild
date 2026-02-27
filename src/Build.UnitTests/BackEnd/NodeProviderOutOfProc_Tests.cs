// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for NodeProviderOutOfProc, specifically the node over-provisioning detection feature.
    /// </summary>
    public class NodeProviderOutOfProc_Tests
    {
        /// <summary>
        /// Test helper class to expose protected methods for testing.
        /// Uses configurable overrides for testing.
        /// </summary>
        private sealed class TestableNodeProviderOutOfProcBase : NodeProviderOutOfProcBase
        {
            private readonly int _systemWideNodeCount;
            private readonly int? _thresholdOverride;

            public TestableNodeProviderOutOfProcBase(int systemWideNodeCount, int? thresholdOverride = null)
            {
                _systemWideNodeCount = systemWideNodeCount;
                _thresholdOverride = thresholdOverride;
            }

            protected override int GetNodeReuseThreshold()
            {
                // If threshold is overridden, use it; otherwise use base implementation
                return _thresholdOverride ?? base.GetNodeReuseThreshold();
            }

            protected override int CountSystemWideActiveNodes()
            {
                return _systemWideNodeCount;
            }

            public bool[] TestDetermineNodesForReuse(int nodeCount, bool enableReuse)
            {
                return DetermineNodesForReuse(nodeCount, enableReuse);
            }

            public int TestGetNodeReuseThreshold()
            {
                return GetNodeReuseThreshold();
            }
        }

        [Fact]
        public void DetermineNodesForReuse_WhenReuseDisabled_AllNodesShouldTerminate()
        {
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 10, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 3, enableReuse: false);
            
            result.Length.ShouldBe(3);
            result.ShouldAllBe(shouldReuse => shouldReuse == false);
        }

        [Fact]
        public void DetermineNodesForReuse_WhenThresholdIsZero_AllNodesShouldTerminate()
        {
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 10, thresholdOverride: 0);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 3, enableReuse: true);
            
            result.Length.ShouldBe(3);
            result.ShouldAllBe(shouldReuse => shouldReuse == false);
        }

        [Fact]
        public void DetermineNodesForReuse_WhenUnderThreshold_AllNodesShouldBeReused()
        {
            // System has 3 nodes total, threshold is 4, so we're under the limit
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 3, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 3, enableReuse: true);
            
            result.Length.ShouldBe(3);
            result.ShouldAllBe(shouldReuse => shouldReuse == true);
        }

        [Fact]
        public void DetermineNodesForReuse_WhenAtThreshold_AllNodesShouldBeReused()
        {
            // System has 4 nodes total, threshold is 4, so we're at the limit
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 4, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 4, enableReuse: true);
            
            result.Length.ShouldBe(4);
            result.ShouldAllBe(shouldReuse => shouldReuse == true);
        }

        [Fact]
        public void DetermineNodesForReuse_WhenOverThreshold_ExcessNodesShouldTerminate()
        {
            // System has 10 nodes total, threshold is 4
            // This instance has 3 nodes
            // We should keep 0 nodes from this instance (since 10 - 3 = 7, which is already > threshold)
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 10, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 3, enableReuse: true);
            
            result.Length.ShouldBe(3);
            result.ShouldAllBe(shouldReuse => shouldReuse == false);
        }

        [Fact]
        public void DetermineNodesForReuse_WhenSlightlyOverThreshold_SomeNodesShouldBeReused()
        {
            // System has 6 nodes total, threshold is 4
            // This instance has 3 nodes
            // Other instances have 6 - 3 = 3 nodes
            // We need to reduce by 2 nodes to reach threshold
            // So we should keep 1 node from this instance
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 6, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 3, enableReuse: true);
            
            result.Length.ShouldBe(3);
            // First node should be reused, others should terminate
            result[0].ShouldBeTrue();
            result[1].ShouldBeFalse();
            result[2].ShouldBeFalse();
        }

        [Fact]
        public void DetermineNodesForReuse_WithSingleNode_BehavesCorrectly()
        {
            // System has 5 nodes total, threshold is 4
            // This instance has 1 node
            // We're over threshold, but only by 1
            // We should terminate this node since others already meet threshold
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 5, thresholdOverride: 4);
            
            bool[] result = provider.TestDetermineNodesForReuse(nodeCount: 1, enableReuse: true);
            
            result.Length.ShouldBe(1);
            result[0].ShouldBeFalse();
        }

        [Fact]
        public void GetNodeReuseThreshold_DefaultImplementation_ReturnsHalfOfCoreCount()
        {
            // Test the default implementation by not providing a threshold override
            // Note: This test uses the actual system core count, so results vary by machine,
            // but the mathematical relationship (threshold = max(1, cores/2)) should hold on all systems
            int coreCount = NativeMethodsShared.GetLogicalCoreCount();
            int expectedThreshold = Math.Max(1, coreCount / 2);
            
            // Create a provider WITHOUT threshold override to test the base class implementation
            var provider = new TestableNodeProviderOutOfProcBase(systemWideNodeCount: 0, thresholdOverride: null);
            
            // The threshold from the provider should match our expected calculation
            int actualThreshold = provider.TestGetNodeReuseThreshold();
            actualThreshold.ShouldBe(expectedThreshold);
            actualThreshold.ShouldBeGreaterThanOrEqualTo(1);
            actualThreshold.ShouldBeLessThanOrEqualTo(coreCount);
        }
    }
}
