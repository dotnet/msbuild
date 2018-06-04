// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Build.BackEnd
{
    #region Delegates
    /// <summary>
    /// Used to receive link status updates from an endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint invoking the delegate.</param>
    /// <param name="status">The current status of the link.</param>
    internal delegate void LinkStatusChangedDelegate(INodeEndpoint endpoint, LinkStatus status);

    /// <summary>
    /// Used to receive data from a node 
    /// </summary>
    /// <param name="endpoint">The endpoint invoking the delegate.</param>
    /// <param name="packet">The packet received.</param>
    internal delegate void DataReceivedDelegate(INodeEndpoint endpoint, INodePacket packet);
    #endregion

    #region Enums
    /// <summary>
    /// The connection status of a link between the NodeEndpoint on the host and the NodeEndpoint
    /// on the peer.
    /// </summary>
    internal enum LinkStatus
    {
        /// <summary>
        /// The connection has never been started.
        /// </summary>
        Inactive,

        /// <summary>
        /// The connection is active, the most recent data has been successfully sent, and the 
        /// node is responding to pings.
        /// </summary>
        Active,

        /// <summary>
        /// The connection has failed and been terminated.
        /// </summary>
        Failed,

        /// <summary>
        /// The connection could not be made/timed out.
        /// </summary>
        ConnectionFailed,
    }

    #endregion

    /// <summary>
    /// This interface represents one end of a connection between the INodeProvider and a Node.
    /// Implementations of this interface define the actual mechanism by which data is communicated.
    /// </summary>
    internal interface INodeEndpoint
    {
        #region Events

        /// <summary>
        /// Raised when the status of the node's link has changed.
        /// </summary>
        event LinkStatusChangedDelegate OnLinkStatusChanged;

        #endregion

        #region Properties

        /// <summary>
        /// The current link status for this endpoint.
        /// </summary>
        LinkStatus LinkStatus
        {
            get;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Waits for the remote node to establish a connection.
        /// </summary>
        /// <param name="factory">The factory used to deserialize packets.</param>
        /// <remarks>Only one of Listen() or Connect() may be called on an endpoint.</remarks>
        void Listen(INodePacketFactory factory);

        /// <summary>
        /// Instructs the node to connect to its peer endpoint.
        /// </summary>
        /// <param name="factory">The factory used to deserialize packets.</param>
        void Connect(INodePacketFactory factory);

        /// <summary>
        /// Instructs the node to disconnect from its peer endpoint.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Sends a data packet to the node.
        /// </summary>
        /// <param name="packet">The packet to be sent.</param>
        void SendData(INodePacket packet);
        #endregion
    }
}
