// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Build.BackEnd;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Unit tests for <see cref="NodeManager"/> node-lifecycle bookkeeping.
    /// These lock in the teardown-serialization contract introduced to fix dotnet/msbuild#12438:
    /// routing a <see cref="NodeShutdown"/> packet must NOT remove the node from the manager's
    /// node-to-provider map (doing so on the pipe read/IO thread races the scheduler's SendData
    /// and throws "Invalid node id specified"). Removal happens only via the serialized
    /// <see cref="NodeManager.RemoveNode"/>, called from BuildManager.HandleNodeShutdown under the
    /// build's sync lock.
    /// </summary>
    public class NodeManager_Tests
    {
        /// <summary>
        /// <see cref="NodeManager.RemoveNode"/> must drop both the owning provider's node context
        /// (via <see cref="INodeProvider.RemoveNodeContext"/>) and the manager's own mapping.
        /// </summary>
        [Fact]
        public void RemoveNode_RemovesProviderMappingAndContext()
        {
            NodeManager manager = CreateNodeManager();
            StubNodeProvider provider = new();
            const int NodeId = 2;
            MapNodeToProvider(manager, NodeId, provider);

            IsNodeMapped(manager, NodeId).ShouldBeTrue();

            manager.RemoveNode(NodeId);

            provider.RemovedContextNode.ShouldBe(NodeId);
            IsNodeMapped(manager, NodeId).ShouldBeFalse();
        }

        /// <summary>
        /// Regression for dotnet/msbuild#12438: routing a NodeShutdown must still deliver the packet
        /// to the handler but must leave the node mapped. Removing it here (as the pre-fix code did)
        /// races a concurrent scheduler SendData on the work-queue thread.
        /// </summary>
        [Fact]
        public void RoutePacket_NodeShutdown_DoesNotRemoveMapping()
        {
            NodeManager manager = CreateNodeManager();
            StubPacketHandler handler = new();
            manager.RegisterPacketHandler(NodePacketType.NodeShutdown, NodeShutdown.FactoryForDeserialization, handler);

            StubNodeProvider provider = new();
            const int NodeId = 2;
            MapNodeToProvider(manager, NodeId, provider);

            manager.RoutePacket(NodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));

            handler.ReceivedNode.ShouldBe(NodeId);
            IsNodeMapped(manager, NodeId).ShouldBeTrue();
        }

        /// <summary>
        /// <see cref="NodeManager.RemoveNode"/> is idempotent: removing an unknown / already-removed
        /// node is a no-op, so a duplicate shutdown never throws.
        /// </summary>
        [Fact]
        public void RemoveNode_UnknownNode_IsNoOp()
        {
            NodeManager manager = CreateNodeManager();

            Should.NotThrow(() => manager.RemoveNode(42));
        }

        private static NodeManager CreateNodeManager()
            => (NodeManager)NodeManager.CreateComponent(BuildComponentType.NodeManager);

        private static ConcurrentDictionary<int, INodeProvider> GetNodeIdToProvider(NodeManager manager)
        {
            FieldInfo field = typeof(NodeManager).GetField("_nodeIdToProvider", BindingFlags.NonPublic | BindingFlags.Instance);
            field.ShouldNotBeNull("NodeManager._nodeIdToProvider field not found; the field may have been renamed.");
            return (ConcurrentDictionary<int, INodeProvider>)field.GetValue(manager);
        }

        private static void MapNodeToProvider(NodeManager manager, int nodeId, INodeProvider provider)
            => GetNodeIdToProvider(manager)[nodeId] = provider;

        private static bool IsNodeMapped(NodeManager manager, int nodeId)
            => GetNodeIdToProvider(manager).ContainsKey(nodeId);

        private sealed class StubNodeProvider : INodeProvider
        {
            public int LastSentNode { get; private set; } = -1;

            public int RemovedContextNode { get; private set; } = -1;

            public NodeProviderType ProviderType => NodeProviderType.OutOfProc;

            public int AvailableNodes => 0;

            public IList<NodeInfo> CreateNodes(int nextNodeId, INodePacketFactory packetFactory, Func<NodeInfo, NodeConfiguration> configurationFactory, int numberOfNodesToCreate)
                => throw new NotImplementedException();

            public void SendData(int node, INodePacket packet) => LastSentNode = node;

            public void RemoveNodeContext(int nodeId) => RemovedContextNode = nodeId;

            public void ShutdownConnectedNodes(bool enableReuse)
            {
            }

            public void ShutdownAllNodes()
            {
            }

            public IEnumerable<Process> GetProcesses() => Array.Empty<Process>();

            public void InitializeComponent(IBuildComponentHost host)
            {
            }

            public void ShutdownComponent()
            {
            }
        }

        private sealed class StubPacketHandler : INodePacketHandler
        {
            public int ReceivedNode { get; private set; } = -1;

            public void PacketReceived(int node, INodePacket packet) => ReceivedNode = node;
        }
    }
}
