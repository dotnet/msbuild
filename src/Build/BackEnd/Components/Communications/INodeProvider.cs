// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The type of nodes provided by the node provider.
    /// </summary>
    internal enum NodeProviderType
    {
        /// <summary>
        /// The provider provides the in-proc node.
        /// </summary>
        InProc,

        /// <summary>
        /// The provider provides out-of-proc nodes.
        /// </summary>
        OutOfProc,

        /// <summary>
        /// The provider provides remote nodes.
        /// </summary>
        Remote
    }

    /// <summary>
    /// This interface represents a collection of nodes in the system.  It provides methods to
    /// enumerate active nodes as well as send data and receive events from those nodes.
    /// </summary>
    internal interface INodeProvider : IBuildComponent
    {
        #region Properties

        /// <summary>
        /// The type of nodes provided by this node provider.
        /// </summary>
        NodeProviderType ProviderType
        {
            get;
        }

        /// <summary>
        /// The number of nodes this provider can create.
        /// </summary>
        int AvailableNodes
        {
            get;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Requests that a new node be created on the specified machine.
        /// </summary>
        /// <param name="nextNodeId">The id to assign to the first created node. Resulting nodes ids will be in range [nextNodeId, nextNodeId + numberOfNodesToCreate - 1]</param>
        /// <param name="packetFactory">
        /// The packet factory used to create packets when data is
        /// received on this node.
        /// </param>
        /// <param name="configurationFactory">NodeConfiguration factory of particular node</param>
        /// <param name="numberOfNodesToCreate">Required number of nodes to create</param>
        /// <returns>Array of NodeInfo of successfully created nodes</returns>
        IList<NodeInfo> CreateNodes(int nextNodeId, INodePacketFactory packetFactory, Func<NodeInfo, NodeConfiguration> configurationFactory, int numberOfNodesToCreate);

        /// <summary>
        /// Sends data to a specific node.
        /// </summary>
        /// <param name="node">The node to which data should be sent.</param>
        /// <param name="packet">The packet to be sent.</param>
        void SendData(int node, INodePacket packet);

        /// <summary>
        /// Shuts down all of the connected, managed nodes.  This call will not return until all nodes are shut down.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        void ShutdownConnectedNodes(bool enableReuse);

        /// <summary>
        /// Shuts down all of the managed nodes.  This call will not return until all nodes are shut down.
        /// </summary>
        void ShutdownAllNodes();

        IEnumerable<Process> GetProcesses();
        #endregion
    }
}
