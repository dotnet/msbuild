// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Execution;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Represents a collection of all node providers in the system.  Reports events concerning
    /// the topology of the system and provides a means to send and receive data to nodes.
    /// </summary>
    internal interface INodeManager : IBuildComponent,
                                      INodePacketFactory
    {
        #region Methods

        /// <summary>
        /// Requests that a new node be created.
        /// </summary>
        /// <param name="configuration">The configuration to use to create the node.</param>
        /// <param name="affinity">The <see cref="NodeAffinity"/> to use.</param>
        /// <returns>Information about the node created</returns>
        /// <remarks>
        /// Throws an exception if the node could not be created.
        /// </remarks>
        NodeInfo CreateNode(NodeConfiguration configuration, NodeAffinity affinity);

        /// <summary>
        /// Sends a data packet to a specific node
        /// </summary>
        /// <param name="node">The node to which the data packet should be sent.</param>
        /// <param name="packet">The packet to send.</param>
        void SendData(int node, INodePacket packet);

        /// <summary>
        /// Shuts down all of the managed nodes.  This is an asynchronous method - the nodes are
        /// not considered shut down until a NodeShutdown packet has been received.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        void ShutdownConnectedNodes(bool enableReuse);

        /// <summary>
        /// Shuts down all of the managed nodes permanently.  This is an asynchronous method - the nodes are
        /// not considered shut down until a NodeShutdown packet has been received.
        /// </summary>
        void ShutdownAllNodes();

        /// <summary>
        /// The node manager contains state which is not supposed to persist between builds, make sure this is cleared.
        /// </summary>
        void ClearPerBuildState();
        #endregion
    }
}
