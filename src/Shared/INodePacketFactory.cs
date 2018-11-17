// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// A delegate representing factory methods used to re-create packets deserialized from a stream.
    /// </summary>
    /// <param name="translator">The translator containing the packet data.</param>
    /// <returns>The packet reconstructed from the stream.</returns>
    internal delegate INodePacket NodePacketFactoryMethod(ITranslator translator);

    /// <summary>
    /// This interface represents an object which is used to reconstruct packet objects from
    /// binary data.
    /// </summary>
    internal interface INodePacketFactory
    {
        #region Methods

        /// <summary>
        /// Registers the specified handler for a particular packet type.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        /// <param name="factory">The factory for packets of the specified type.</param>
        /// <param name="handler">The handler to be called when packets of the specified type are received.</param>
        void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler);

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type.</param>
        void UnregisterPacketHandler(NodePacketType packetType);

        /// <summary>
        /// Takes a serializer, deserializes the packet and routes it to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator containing the data from which the packet should be reconstructed.</param>
        void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator);

        /// <summary>
        /// Routes the specified packet
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        void RoutePacket(int nodeId, INodePacket packet);

        #endregion
    }
}
