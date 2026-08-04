// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Sent by a node that stays connected to its owner across builds, once it has disposed the
    /// state of the build that just completed and is ready to serve the next one.
    /// </summary>
    /// <remarks>
    /// A node that exits at the end of a build reports that with <see cref="NodeShutdown"/>, and its
    /// owner treats that packet as the end of the connection. A node that stays connected has to say
    /// "this build is done" without saying "this connection is done"; reusing
    /// <see cref="NodeShutdown"/> for both would force every reader of that packet to work out which
    /// was meant.
    ///
    /// Sidecar TaskHosts are the first users. Worker nodes have the same end-of-build shape, so if
    /// they later stay connected to their owner they should send this packet rather than introduce
    /// another one that means the same thing.
    /// </remarks>
    internal sealed class NodeReadyForNextBuild : INodePacket
    {
        public NodePacketType Type => NodePacketType.NodeReadyForNextBuild;

        public void Translate(ITranslator translator)
        {
            // The packet carries no payload: its arrival is the whole message.
        }

        internal static INodePacket FactoryForDeserialization(ITranslator translator)
        {
            NodeReadyForNextBuild packet = new();
            packet.Translate(translator);
            return packet;
        }
    }
}
