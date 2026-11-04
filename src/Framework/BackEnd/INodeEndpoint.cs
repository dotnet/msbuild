// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd;

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

/// <summary>
/// This interface represents one end of a connection between the INodeProvider and a Node.
/// Implementations of this interface define the actual mechanism by which data is communicated.
/// </summary>
internal interface INodeEndpoint
{
    /// <summary>
    /// Raised when the status of the node's link has changed.
    /// </summary>
    event LinkStatusChangedDelegate OnLinkStatusChanged;

    /// <summary>
    /// The current link status for this endpoint.
    /// </summary>
    LinkStatus LinkStatus { get; }

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

    /// <summary>
    /// Called when we are about to send last packet to finalize graceful disconnection with client.
    /// This is needed to handle race condition when both client and server is gracefully about to close connection.
    /// </summary>
    void ClientWillDisconnect();
}
