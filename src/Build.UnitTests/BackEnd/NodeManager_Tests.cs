// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for NodeManager, focusing on the thread safety of SendData
    /// when nodes are being removed concurrently.
    /// </summary>
    public class NodeManager_Tests
    {
        /// <summary>
        /// Verifies that SendData does not throw when a node has already been removed
        /// from the provider mapping. This is the core fix for the race condition where
        /// the communication thread removes a node (on NodeShutdown) before the BuildManager
        /// work queue finishes processing earlier packets from that node.
        /// </summary>
        [Fact]
        public void SendData_DoesNotThrow_WhenNodeIsNotRegistered()
        {
            INodeManager nodeManager = (INodeManager)NodeManager.CreateComponent(BuildComponentType.NodeManager);

            // Sending data to a node that was never registered (or has been removed)
            // should not throw. Before the fix, this would throw InternalErrorException
            // with "Node 2 does not have a provider".
            var packet = new TestPacket();
            Should.NotThrow(() => nodeManager.SendData(2, packet));
        }

        /// <summary>
        /// Verifies that RoutePacket with a NodeShutdown packet removes the node from
        /// the provider mapping and subsequent SendData calls are safe.
        /// </summary>
        [Fact]
        public void SendData_DoesNotThrow_AfterNodeShutdownRouted()
        {
            NodeManager nodeManager = (NodeManager)NodeManager.CreateComponent(BuildComponentType.NodeManager);

            // Use reflection to add a node to the provider mapping so we can test removal.
            ConcurrentDictionary<int, INodeProvider> nodeIdToProvider = GetNodeIdToProvider(nodeManager);
            var mockProvider = new MockNodeProvider();
            nodeIdToProvider.TryAdd(2, mockProvider);

            // Register a packet handler for NodeShutdown so RoutePacket can route it.
            var handler = new MockPacketHandler();
            nodeManager.RegisterPacketHandler(NodePacketType.NodeShutdown, NodeShutdown.FactoryForDeserialization, handler);

            // Route a NodeShutdown packet, which should remove node 2 from the mapping.
            var shutdownPacket = new NodeShutdown(NodeShutdownReason.Requested);
            nodeManager.RoutePacket(2, shutdownPacket);

            // Now SendData should not throw even though the node was removed.
            var dataPacket = new TestPacket();
            Should.NotThrow(() => nodeManager.SendData(2, dataPacket));
        }

        /// <summary>
        /// Verifies that concurrent calls to RoutePacket (NodeShutdown) and SendData
        /// on the same node do not throw or corrupt state. This simulates the race
        /// condition between the communication thread and the BuildManager work queue.
        /// </summary>
        [Fact]
        public void SendData_IsThreadSafe_WithConcurrentNodeRemoval()
        {
            NodeManager nodeManager = (NodeManager)NodeManager.CreateComponent(BuildComponentType.NodeManager);

            ConcurrentDictionary<int, INodeProvider> nodeIdToProvider = GetNodeIdToProvider(nodeManager);
            var handler = new MockPacketHandler();
            nodeManager.RegisterPacketHandler(NodePacketType.NodeShutdown, NodeShutdown.FactoryForDeserialization, handler);

            // Run multiple iterations to increase the chance of catching race conditions.
            for (int i = 0; i < 100; i++)
            {
                int nodeId = i + 2;
                var mockProvider = new MockNodeProvider();
                nodeIdToProvider.TryAdd(nodeId, mockProvider);

                using var barrier = new ManualResetEventSlim(false);

                // One thread removes the node via RoutePacket (simulating communication thread).
                Task removeTask = Task.Run(() =>
                {
                    barrier.Wait();
                    var shutdownPacket = new NodeShutdown(NodeShutdownReason.Requested);
                    nodeManager.RoutePacket(nodeId, shutdownPacket);
                });

                // Another thread tries to send data (simulating BuildManager work queue).
                Task sendTask = Task.Run(() =>
                {
                    barrier.Wait();
                    var dataPacket = new TestPacket();
                    nodeManager.SendData(nodeId, dataPacket);
                });

                // Release both threads at the same time.
                barrier.Set();

                // Neither thread should throw.
                Should.NotThrow(() => Task.WaitAll(removeTask, sendTask));
            }
        }

        private static ConcurrentDictionary<int, INodeProvider> GetNodeIdToProvider(NodeManager nodeManager)
        {
            FieldInfo field = typeof(NodeManager).GetField("_nodeIdToProvider", BindingFlags.NonPublic | BindingFlags.Instance);
            field.ShouldNotBeNull();
            return (ConcurrentDictionary<int, INodeProvider>)field.GetValue(nodeManager);
        }

        private sealed class TestPacket : INodePacket
        {
            public NodePacketType Type => NodePacketType.BuildRequestConfigurationResponse;

            public void Translate(ITranslator translator)
            {
            }
        }

        private sealed class MockNodeProvider : INodeProvider
        {
            public int AvailableNodes => 0;

            public NodeProviderType ProviderType => NodeProviderType.OutOfProc;

            public IList<NodeInfo> CreateNodes(int nextNodeId, INodePacketFactory factory, Func<NodeInfo, NodeConfiguration> configurationFactory, int numberOfNodesToCreate)
                => [];

            public void SendData(int nodeId, INodePacket packet)
            {
                // No-op for testing
            }

            public void ShutdownConnectedNodes(bool enableReuse)
            {
            }

            public void ShutdownAllNodes()
            {
            }

            public IEnumerable<Process> GetProcesses() => [];

            public void InitializeComponent(IBuildComponentHost host)
            {
            }

            public void ShutdownComponent()
            {
            }
        }

        private sealed class MockPacketHandler : INodePacketHandler
        {
            public void PacketReceived(int node, INodePacket packet)
            {
                // No-op for testing
            }
        }
    }
}
