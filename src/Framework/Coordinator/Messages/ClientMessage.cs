// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Base type for all messages sent from an MSBuild client to the coordinator.
/// </summary>
internal abstract record ClientMessage
{
    public abstract ClientMessageType MessageType { get; }

    public static ClientMessage ReadFrom(BinaryReader reader)
    {
        var messageType = (ClientMessageType)reader.ReadByte();

        return messageType switch
        {
            ClientMessageType.Handshake => ClientHandshakeMessage.ReadPayload(reader),
            ClientMessageType.RequestNodes => RequestNodesMessage.ReadPayload(reader),
            ClientMessageType.ReleaseNodes => ReleaseNodesMessage.Instance,
            ClientMessageType.Heartbeat => HeartbeatMessage.Instance,
            ClientMessageType.JoinGrant => JoinGrantMessage.ReadPayload(reader),
            ClientMessageType.RequestNodesWithPriority => RequestNodesWithPriorityMessage.ReadPayload(reader),

            _ => Assumed.Unreachable<ClientMessage>($"Unknown client message type: {messageType}"),
        };
    }

    public void WriteTo(BinaryWriter writer)
    {
        writer.Write((byte)MessageType);
        WritePayload(writer);
        writer.Flush();
    }

    protected virtual void WritePayload(BinaryWriter writer)
    {
        // Descendants can override.
    }
}
