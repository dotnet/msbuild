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
    internal class TaskHostNodeManager : INodeManager
    {
        /// <summary>
        /// The node provider for task hosts. 
        /// </summary>
        private INodeProvider _outOfProcTaskHostNodeProvider;

        /// <summary>
        /// The build component host.
        /// </summary>
        private IBuildComponentHost _componentHost;

        /// <summary>
        /// Tracks whether ShutdownComponent has been called.  
        /// </summary>
        private bool _componentShutdown;

        /// <summary>
        /// Constructor.
        /// </summary>
        private TaskHostNodeManager()
        {
            // do nothing
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
            throw new NotSupportedException("not used");
        }

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="packet">The packet to send.</param>
        public void SendData(int node, INodePacket packet)
        {
            throw new NotSupportedException("not used");
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        public void ShutdownConnectedNodes(bool enableReuse)
        {
            ErrorUtilities.VerifyThrow(!_componentShutdown, "We should never be calling ShutdownNodes after ShutdownComponent has been called");
            _outOfProcTaskHostNodeProvider?.ShutdownConnectedNodes(enableReuse);
        }

        /// <summary>
        /// Shuts down all of the managed nodes permanently.
        /// </summary>
        public void ShutdownAllNodes()
        {
            _outOfProcTaskHostNodeProvider?.ShutdownAllNodes();
        }
        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Initializes the component
        /// </summary>
        /// <param name="host">The component host</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            ErrorUtilities.VerifyThrow(_componentHost == null, "TaskHostNodeManager already initialized.");
            ErrorUtilities.VerifyThrow(host != null, "We can't create a TaskHostNodeManager with a null componentHost");

            _componentHost = host;
            _outOfProcTaskHostNodeProvider = _componentHost.GetComponent(BuildComponentType.OutOfProcTaskHostNodeProvider) as INodeProvider;
            _componentShutdown = false;
        }

        /// <summary>
        /// Shuts down the component.
        /// </summary>
        public void ShutdownComponent()
        {
            _outOfProcTaskHostNodeProvider = null;
            _componentHost = null;
            _componentShutdown = true;

            ClearPerBuildState();
        }

        /// <summary>
        /// Reset the state of objects in the node manager which need to be reset between builds.
        /// </summary>
        public void ClearPerBuildState()
        {
            // do nothing
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
            throw new NotSupportedException("not used");
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        public void UnregisterPacketHandler(NodePacketType packetType)
        {
            throw new NotSupportedException("not used");
        }

        /// <summary>
        /// Takes a serializer, deserializes the packet and routes it to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator containing the data from which the packet should be reconstructed.</param>
        public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
        {
            throw new NotSupportedException("not used");
        }

        /// <summary>
        /// Routes the specified packet. This is called by the Inproc node directly since it does not have to do any deserialization
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            throw new NotSupportedException("not used");
        }

        #endregion

        /// <summary>
        /// Factory for component creation.
        /// </summary>
        static internal IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrow(type == BuildComponentType.TaskHostNodeManager, "Cannot create component of type {0}", type);
            return new TaskHostNodeManager();
        }
    }
}
