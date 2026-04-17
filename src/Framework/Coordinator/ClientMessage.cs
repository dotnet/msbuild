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

    public static ClientMessage Read(BinaryReader reader)
    {
        byte version = reader.ReadByte();

        if (version != Protocol.Version)
        {
            throw new InternalErrorException($"Unsupported coordinator protocol version: {version} (expected {Protocol.Version})");
        }

        var messageType = (ClientMessageType)reader.ReadByte();

        return messageType switch
        {
            ClientMessageType.RequestNodes => new RequestNodesMessage(requestedNodes: reader.ReadInt32(), processId: reader.ReadInt32()),
            ClientMessageType.ReleaseNodes => ReleaseNodesMessage.Instance,
            ClientMessageType.Heartbeat => HeartbeatMessage.Instance,

            _ => throw new InternalErrorException($"Unknown client message type: {messageType}"),
        };
    }

    public void Write(BinaryWriter writer)
    {
        writer.Write(Protocol.Version);
        writer.Write((byte)MessageType);
        WritePayload(writer);
        writer.Flush();
    }

    protected virtual void WritePayload(BinaryWriter writer)
    {
        // Descendants can override.
    }
}
