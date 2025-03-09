// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Implementation of INodePacketFactory as a helper class for classes which expose this interface publicly.
    /// </summary>
    internal class NodePacketFactory : INodePacketFactory
    {
        /// <summary>
        /// Mapping of packet types to factory information.
        /// </summary>
        private Dictionary<NodePacketType, PacketFactoryRecord> _packetFactories;

        /// <summary>
        /// Constructor
        /// </summary>
        public NodePacketFactory()
        {
            _packetFactories = new Dictionary<NodePacketType, PacketFactoryRecord>();
        }

        #region INodePacketFactory Members

        /// <summary>
        /// Registers a packet handler
        /// </summary>
        public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            _packetFactories[packetType] = new PacketFactoryRecord(handler, factory);
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        public void UnregisterPacketHandler(NodePacketType packetType)
        {
            _packetFactories.Remove(packetType);
        }

        /// <summary>
        /// Creates a packet with data from a binary stream.
        /// </summary>
        public INodePacket DeserializePacket(NodePacketType packetType, ITranslator translator)
        {
            // PERF: Not using VerifyThrow to avoid boxing of packetType in the non-error case
            if (!_packetFactories.TryGetValue(packetType, out PacketFactoryRecord record))
            {
                ErrorUtilities.ThrowInternalError("No packet handler for type {0}", packetType);
            }

            return record.DeserializePacket(translator);
        }

        /// <summary>
        /// Routes the specified packet.
        /// </summary>
        public void RoutePacket(int nodeId, INodePacket packet)
        {
            // PERF: Not using VerifyThrow to avoid boxing of packetType in the non-error case
            if (!_packetFactories.TryGetValue(packet.Type, out PacketFactoryRecord record))
            {
                ErrorUtilities.ThrowInternalError("No packet handler for type {0}", packet.Type);
            }

            record.RoutePacket(nodeId, packet);
        }

        #endregion

        /// <summary>
        /// A record for a packet factory
        /// </summary>
        private class PacketFactoryRecord
        {
            /// <summary>
            /// The handler to invoke when the packet is deserialized.
            /// </summary>
            private INodePacketHandler _handler;

            /// <summary>
            /// The method used to construct a packet from a translator stream.
            /// </summary>
            private NodePacketFactoryMethod _factoryMethod;

            /// <summary>
            /// Constructor.
            /// </summary>
            public PacketFactoryRecord(INodePacketHandler handler, NodePacketFactoryMethod factoryMethod)
            {
                _handler = handler;
                _factoryMethod = factoryMethod;
            }

            /// <summary>
            /// Creates a packet from a binary stream.
            /// </summary>
            public INodePacket DeserializePacket(ITranslator translator) => _factoryMethod(translator);

            /// <summary>
            /// Routes the packet to the correct destination.
            /// </summary>
            public void RoutePacket(int nodeId, INodePacket packet)
            {
                _handler.PacketReceived(nodeId, packet);
            }
        }
    }
}
