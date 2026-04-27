// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Base type for all messages sent from the coordinator to an MSBuild client.
/// </summary>
internal abstract record ServerMessage
{
    public abstract ServerMessageType MessageType { get; }

    public static ServerMessage ReadFrom(BinaryReader reader)
    {
        byte version = reader.ReadByte();

        if (version != Protocol.Version)
        {
            throw new InternalErrorException($"Unsupported coordinator protocol version: {version} (expected {Protocol.Version})");
        }

        var messageType = (ServerMessageType)reader.ReadByte();

        return messageType switch
        {
            ServerMessageType.NodeGrant => new NodeGrantMessage(grantedNodes: reader.ReadInt32()),
            ServerMessageType.Wait => WaitMessage.Instance,
            ServerMessageType.Error => new ErrorMessage(message: reader.ReadString()),

            _ => throw new InternalErrorException($"Unknown coordinator message type: {messageType}"),
        };
    }

    public void WriteTo(BinaryWriter writer)
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
