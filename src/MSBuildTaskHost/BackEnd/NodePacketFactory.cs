// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.TaskHost.Utilities;

namespace Microsoft.Build.TaskHost.BackEnd;

/// <summary>
/// Implementation of INodePacketFactory as a helper class for classes which expose this interface publicly.
/// </summary>
internal sealed class NodePacketFactory : INodePacketFactory
{
    /// <summary>
    /// Mapping of packet types to factory information.
    /// </summary>
    private readonly Dictionary<NodePacketType, PacketFactoryRecord> _packetFactories;

    public NodePacketFactory()
    {
        _packetFactories = new Dictionary<NodePacketType, PacketFactoryRecord>();
    }

    /// <summary>
    /// Registers a packet handler.
    /// </summary>
    public void RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        => _packetFactories[packetType] = new PacketFactoryRecord(handler, factory);

    /// <summary>
    /// Unregisters a packet handler.
    /// </summary>
    public void UnregisterPacketHandler(NodePacketType packetType)
        => _packetFactories.Remove(packetType);

    /// <summary>
    /// Creates and routes a packet with data from a binary stream.
    /// </summary>
    public void DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, ITranslator translator)
    {
        if (!_packetFactories.TryGetValue(packetType, out PacketFactoryRecord? record))
        {
            ErrorUtilities.ThrowInternalError($"No packet handler for type {packetType}");
        }

        INodePacket packet = record.DeserializePacket(translator);
        record.RoutePacket(nodeId, packet);
    }

    /// <summary>
    /// Creates a packet with data from a binary stream.
    /// </summary>
    public INodePacket DeserializePacket(NodePacketType packetType, ITranslator translator)
    {
        if (!_packetFactories.TryGetValue(packetType, out PacketFactoryRecord? record))
        {
            ErrorUtilities.ThrowInternalError($"No packet handler for type {packetType}");
        }

        return record.DeserializePacket(translator);
    }

    /// <summary>
    /// Routes the specified packet.
    /// </summary>
    public void RoutePacket(int nodeId, INodePacket packet)
    {
        if (!_packetFactories.TryGetValue(packet.Type, out PacketFactoryRecord record))
        {
            ErrorUtilities.ThrowInternalError($"No packet handler for type {packet.Type}");
        }

        record.RoutePacket(nodeId, packet);
    }

    /// <summary>
    /// A record for a packet factory.
    /// </summary>
    /// <param name="handler">The handler to invoke when the packet is deserialized.</param>
    /// <param name="factoryMethod">The method used to construct a packet from a translator stream.</param>
    private sealed class PacketFactoryRecord(INodePacketHandler handler, NodePacketFactoryMethod factoryMethod)
    {
        /// <summary>
        /// Creates a packet from a binary stream.
        /// </summary>
        public INodePacket DeserializePacket(ITranslator translator)
            => factoryMethod(translator);

        /// <summary>
        /// Routes the packet to the correct destination.
        /// </summary>
        public void RoutePacket(int nodeId, INodePacket packet)
            => handler.PacketReceived(nodeId, packet);
    }
}
