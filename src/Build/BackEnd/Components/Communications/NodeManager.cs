﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The NodeManager class is responsible for marshalling data to/from the NodeProviders and organizing the 
    /// creation of new nodes on request.
    /// </summary>
    internal class NodeManager : INodeManager
    {
        /// <summary>
        /// The node provider for the in-proc node.
        /// </summary>
        private INodeProvider? _inProcNodeProvider;

        /// <summary>
        /// The node provider for out-of-proc nodes.
        /// </summary> 
        private INodeProvider? _outOfProcNodeProvider;

        /// <summary>
        /// The build component host.
        /// </summary>
        private IBuildComponentHost? _componentHost;

        /// <summary>
        /// Mapping of manager-produced node IDs to the provider hosting the node.
        /// </summary>
        private readonly Dictionary<int, INodeProvider> _nodeIdToProvider;

        /// <summary>
        /// The packet factory used to translate and route packets
        /// </summary>
        private NodePacketFactory _packetFactory;

        /// <summary>
        /// The next node id to assign to a node.
        /// </summary>
        private int _nextNodeId;

        /// <summary>
        /// The nodeID for the inproc node.
        /// </summary>
        private int _inprocNodeId = 1;

        /// <summary>
        /// Flag indicating when the nodes have been shut down.
        /// BUGBUG: This is a fix which corrects an RI blocking BVT failure.  The real fix must be determined before RTM.
        /// This must be investigated and resolved before RTM.  The apparent issue is that a design-time build has already called EndBuild
        /// through the BuildManagerAccessor, and the nodes are shut down.  Shortly thereafter, the solution build manager comes through and calls EndBuild, which throws
        /// another Shutdown packet in the queue, and causes the following build to stop prematurely.  This is all timing related - not every sequence of builds seems to 
        /// cause the problem, probably due to the order in which the packet queue gets serviced relative to other threads.
        /// 
        /// It appears that the problem is that the BuildRequestEngine is being invoked in a way that causes a shutdown packet to appear to overlap with a build request packet.
        /// Interactions between the in-proc node communication thread and the shutdown mechanism must be investigated to determine how BuildManager.EndBuild is allowing itself
        /// to return before the node has indicated it is actually finished.
        /// </summary>
        private bool _nodesShutdown = true;

        /// <summary>
        /// Tracks whether ShutdownComponent has been called.  
        /// </summary>
        private bool _componentShutdown;

        /// <summary>
        /// Constructor.
        /// </summary>
        private NodeManager()
        {
            _nodeIdToProvider = new Dictionary<int, INodeProvider>();
            _packetFactory = new NodePacketFactory();
            _nextNodeId = _inprocNodeId + 1;
        }

        #region INodeManager Members

        /// <summary>
        /// Creates a node on an available NodeProvider, if any..
        /// </summary>
        /// <param name="configuration">The configuration to use for the remote node.</param>
        /// <param name="nodeAffinity">The <see cref="NodeAffinity"/> to use.</param>
        /// <param name="numberOfNodesToCreate">Number of nodes to be reused ot created.</param>
        /// <returns>A NodeInfo describing the node created, or null if none could be created.</returns>
        public IList<NodeInfo> CreateNodes(NodeConfiguration configuration, NodeAffinity nodeAffinity, int numberOfNodesToCreate)
        {
            // We will prefer to make nodes on the "closest" providers first; in-proc, then
            // out-of-proc, then remote.
            // When we support distributed build, we will also consider the remote provider.
            List<NodeInfo> nodes = new(numberOfNodesToCreate);
            if ((nodeAffinity == NodeAffinity.Any || nodeAffinity == NodeAffinity.InProc) && !_componentHost!.BuildParameters.DisableInProcNode)
            {
                nodes.AddRange(AttemptCreateNode(_inProcNodeProvider!, configuration, numberOfNodesToCreate));
            }

            if (nodes.Count < numberOfNodesToCreate && (nodeAffinity == NodeAffinity.Any || nodeAffinity == NodeAffinity.OutOfProc))
            {
                nodes.AddRange(AttemptCreateNode(_outOfProcNodeProvider!, configuration, numberOfNodesToCreate - nodes.Count));
            }

            // If we created a node, they should no longer be considered shut down.
            if (nodes.Count > 0)
            {
                _nodesShutdown = false;
            }

            return nodes;
        }

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="packet">The packet to send.</param>
        public void SendData(int node, INodePacket packet)
        {
            if (!_nodeIdToProvider.TryGetValue(node, out INodeProvider? provider))
            {
                ErrorUtilities.ThrowInternalError("Node {0} does not have a provider.", node);
            }
            else
            {
                provider.SendData(node, packet);
            }
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        public void ShutdownConnectedNodes(bool enableReuse)
        {
            ErrorUtilities.VerifyThrow(!_componentShutdown, "We should never be calling ShutdownNodes after ShutdownComponent has been called");

            if (_nodesShutdown)
            {
                return;
            }

            _nodesShutdown = true;
            _inProcNodeProvider?.ShutdownConnectedNodes(enableReuse);
            _outOfProcNodeProvider?.ShutdownConnectedNodes(enableReuse);
        }

        /// <summary>
        /// Shuts down all of managed nodes permanently.
        /// </summary>
        public void ShutdownAllNodes()
        {
            // don't worry about inProc
            _outOfProcNodeProvider?.ShutdownAllNodes();
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Initializes the component
        /// </summary>
        /// <param name="host">The component host</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            ErrorUtilities.VerifyThrow(_componentHost == null, "NodeManager already initialized.");
            ErrorUtilities.VerifyThrow(host != null, "We can't create a NodeManager with a null componentHost");
            _componentHost = host!;

            _inProcNodeProvider = _componentHost.GetComponent(BuildComponentType.InProcNodeProvider) as INodeProvider;
            _outOfProcNodeProvider = _componentHost.GetComponent(BuildComponentType.OutOfProcNodeProvider) as INodeProvider;

            _componentShutdown = false;

            // DISTRIBUTED: Get the remote node provider.
        }

        /// <summary>
        /// Shuts down the component.
        /// </summary>
        public void ShutdownComponent()
        {
            if (_inProcNodeProvider != null && _inProcNodeProvider is IDisposable)
            {
                ((IDisposable)_inProcNodeProvider).Dispose();
            }

            if (_outOfProcNodeProvider != null && _outOfProcNodeProvider is IDisposable)
            {
                ((IDisposable)_outOfProcNodeProvider).Dispose();
            }

            _inProcNodeProvider = null;
            _outOfProcNodeProvider = null;
            _componentHost = null;
            _componentShutdown = true;

            ClearPerBuildState();
        }

        /// <summary>
        /// Reset the state of objects in the node manager which need to be reset between builds.
        /// </summary>
        public void ClearPerBuildState()
        {
            _packetFactory = new NodePacketFactory();
            _nodeIdToProvider.Clear();

            // because the inproc node is always 1 therefore when new nodes are requested we need to start at 2
            _nextNodeId = _inprocNodeId + 1;
        }

        #endregion

        #region INodePacketFactory Members

        /// <summary>
        /// Registers the specified handler for a particular packet type.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        /// <param name="factory">The factory for packets of the specified type.</param>
        /// <param name="handler">The handler to be called when packets of the specified type are received.</param>
        public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            _packetFactory.RegisterPacketHandler(packetType, factory, handler);
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        public void UnregisterPacketHandler(NodePacketType packetType)
        {
            _packetFactory.UnregisterPacketHandler(packetType);
        }

        /// <summary>
        /// Takes a serializer, deserializes the packet and routes it to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator containing the data from which the packet should be reconstructed.</param>
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            if (packetType == NodePacketType.NodeShutdown)
            {
                RemoveNodeFromMapping(nodeId);
            }

            _packetFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
        }

        /// <summary>
        /// Routes the specified packet. This is called by the Inproc node directly since it does not have to do any deserialization
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            if (packet.Type == NodePacketType.NodeShutdown)
            {
                RemoveNodeFromMapping(nodeId);
            }

            _packetFactory.RoutePacket(nodeId, packet);
        }

        #endregion

        /// <summary>
        /// Factory for component creation.
        /// </summary>
        internal static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrow(type == BuildComponentType.NodeManager, "Cannot create component of type {0}", type);
            return new NodeManager();
        }

        /// <summary>
        /// We have received the node shutdown packet for this node, we should remove it from our list of providers.
        /// </summary>
        private void RemoveNodeFromMapping(int nodeId)
        {
            _nodeIdToProvider.Remove(nodeId);
            if (_nodeIdToProvider.Count == 0)
            {
                // The inproc node is always 1 therefore when new nodes are requested we need to start at 2
                _nextNodeId = _inprocNodeId + 1;
            }
        }

        /// <summary>
        /// Attempts to create a node on the specified machine using the specified provider.
        /// </summary>
        /// <param name="nodeProvider">The provider used to create the node.</param>
        /// <param name="nodeConfiguration">The <see cref="NodeConfiguration"/> to use.</param>
        /// <param name="numberOfNodesToCreate">Number of nodes to be reused ot created.</param>
        /// <returns>List of created nodes.</returns>
        private IList<NodeInfo> AttemptCreateNode(INodeProvider nodeProvider, NodeConfiguration nodeConfiguration, int numberOfNodesToCreate)
        {
            // If no provider was passed in, we obviously can't create a node.
            if (nodeProvider == null)
            {
                ErrorUtilities.ThrowInternalError("No node provider provided.");
                return new List<NodeInfo>();
            }

            // Are there any free slots on this provider?
            if (nodeProvider.AvailableNodes == 0)
            {
                return new List<NodeInfo>();
            }

            // Assign a global ID to the node we are about to create.
            int fromNodeId;
            if (nodeProvider is NodeProviderInProc)
            {
                fromNodeId = _inprocNodeId;
            }
            else
            {
                // Reserve node numbers for all needed nodes.
                fromNodeId = Interlocked.Add(ref _nextNodeId, numberOfNodesToCreate) - numberOfNodesToCreate;
            }


            // Create the node and add it to our mapping.
            IList<NodeInfo> nodes = nodeProvider.CreateNodes(fromNodeId, this, AcquiredNodeConfigurationFactory, numberOfNodesToCreate);

            foreach (NodeInfo node in nodes)
            {
                _nodeIdToProvider.Add(node.NodeId, nodeProvider);
            }

            return nodes;

            NodeConfiguration AcquiredNodeConfigurationFactory(NodeInfo nodeInfo)
            {
                var config = nodeConfiguration.Clone();
                config.NodeId = nodeInfo.NodeId;
                return config;
            }
        }

        public IEnumerable<Process> GetProcesses()
        {
            return _outOfProcNodeProvider?.GetProcesses()!;
        }
    }
}
