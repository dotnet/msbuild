// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using Microsoft.Build.Shared;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The NodeManager class is responsible for marshalling data to/from the NodeProviders and organizing the 
    /// creation of new nodes on request.
    /// </summary>
    internal class NodeManager : INodeManager
    {
        /// <summary>
        /// The invalid node id
        /// </summary>
        private const int InvalidNodeId = 0;

        /// <summary>
        /// The node provider for the in-proc node.
        /// </summary>
        private INodeProvider _inProcNodeProvider;

        /// <summary>
        /// The node provider for out-of-proc nodes.
        /// </summary> 
        private INodeProvider _outOfProcNodeProvider;

        /// <summary>
        /// The build component host.
        /// </summary>
        private IBuildComponentHost _componentHost;

        /// <summary>
        /// Mapping of manager-produced node IDs to the provider hosting the node.
        /// </summary>
        private Dictionary<int, INodeProvider> _nodeIdToProvider;

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
        /// <returns>A NodeInfo describing the node created, or null if none could be created.</returns>
        public NodeInfo CreateNode(NodeConfiguration configuration, NodeAffinity nodeAffinity)
        {
            // We will prefer to make nodes on the "closest" providers first; in-proc, then
            // out-of-proc, then remote.
            // When we support distributed build, we will also consider the remote provider.
            int nodeId = InvalidNodeId;
            if ((nodeAffinity == NodeAffinity.Any || nodeAffinity == NodeAffinity.InProc) && !_componentHost.BuildParameters.DisableInProcNode)
            {
                nodeId = AttemptCreateNode(_inProcNodeProvider, configuration);
            }

            if (nodeId == InvalidNodeId && (nodeAffinity == NodeAffinity.Any || nodeAffinity == NodeAffinity.OutOfProc))
            {
                nodeId = AttemptCreateNode(_outOfProcNodeProvider, configuration);
            }

            if (nodeId == InvalidNodeId)
            {
                return null;
            }

            // If we created a node, they should no longer be considered shut down.
            _nodesShutdown = false;

            return new NodeInfo(nodeId, _nodeIdToProvider[nodeId].ProviderType);
        }

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="packet">The packet to send.</param>
        public void SendData(int node, INodePacket packet)
        {
            // Look up the node provider for this node in the mapping.
            INodeProvider provider = null;
            if (!_nodeIdToProvider.TryGetValue(node, out provider))
            {
                ErrorUtilities.ThrowInternalError("Node {0} does not have a provider.", node);
            }

            // Send the data.
            provider.SendData(node, packet);
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

            if (null != _inProcNodeProvider)
            {
                _inProcNodeProvider.ShutdownConnectedNodes(enableReuse);
            }

            if (null != _outOfProcNodeProvider)
            {
                _outOfProcNodeProvider.ShutdownConnectedNodes(enableReuse);
            }
        }

        /// <summary>
        /// Shuts down all of managed nodes permanently.
        /// </summary>
        public void ShutdownAllNodes()
        {
            // don't worry about inProc
            if (null != _outOfProcNodeProvider)
            {
                _outOfProcNodeProvider.ShutdownAllNodes();
            }
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
            _componentHost = host;

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
        static internal IBuildComponent CreateComponent(BuildComponentType type)
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
        /// <returns>The id of the node created.</returns>
        private int AttemptCreateNode(INodeProvider nodeProvider, NodeConfiguration nodeConfiguration)
        {
            // If no provider was passed in, we obviously can't create a node.
            if (null == nodeProvider)
            {
                ErrorUtilities.ThrowInternalError("No node provider provided.");
                return InvalidNodeId;
            }

            // Are there any free slots on this provider?
            if (nodeProvider.AvailableNodes == 0)
            {
                return InvalidNodeId;
            }

            // Assign a global ID to the node we are about to create.
            int nodeId = InvalidNodeId;

            if (nodeProvider is NodeProviderInProc)
            {
                nodeId = _inprocNodeId;
            }
            else
            {
                nodeId = _nextNodeId;
                _nextNodeId++;
            }

            NodeConfiguration configToSend = nodeConfiguration.Clone();
            configToSend.NodeId = nodeId;

            // Create the node and add it to our mapping.
            bool createdNode = nodeProvider.CreateNode(nodeId, this, configToSend);

            if (!createdNode)
            {
                return InvalidNodeId;
            }

            _nodeIdToProvider.Add(nodeId, nodeProvider);
            return nodeId;
        }
    }
}
