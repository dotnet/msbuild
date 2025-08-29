﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Objects which wish to receive packets from the NodePacketRouter must implement this interface.
    /// </summary>
    internal interface INodePacketHandler
    {
        /// <summary>
        /// This method is invoked by the NodePacketRouter when a packet is received and is intended for
        /// this recipient.
        /// </summary>
        /// <param name="node">The node from which the packet was received.</param>
        /// <param name="packet">The packet.</param>
        void PacketReceived(int node, INodePacket packet);
    }
}
