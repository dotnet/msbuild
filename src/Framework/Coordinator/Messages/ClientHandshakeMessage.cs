// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Handshake sent by the client as the first message on a new connection.
///  Advertises the client's identity and capabilities.
/// </summary>
internal sealed record ClientHandshakeMessage : ClientMessage
{
    /// <summary>
    ///  Gets a unique identifier for this connection, used to distinguish clients
    ///  even if OS process IDs are recycled.
    /// </summary>
    public Guid ConnectionId { get; }

    /// <summary>
    ///  Gets the process ID of the MSBuild client.
    /// </summary>
    public int ProcessId { get; }

    /// <summary>
    ///  Gets the capabilities advertised by the client.
    /// </summary>
    public ImmutableArray<string> Capabilities { get; }

    public ClientHandshakeMessage(Guid connectionId, int processId, ImmutableArray<string> capabilities)
        : base(ClientMessageType.Handshake)
    {
        ConnectionId = connectionId;
        ProcessId = processId;
        Capabilities = capabilities.IsDefault ? [] : capabilities;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.WriteGuid(ConnectionId);
        writer.Write(ProcessId);
        writer.Write(Capabilities.Length);

        foreach (string capability in Capabilities)
        {
            writer.Write(capability);
        }
    }

    internal static ClientHandshakeMessage ReadPayload(BinaryReader reader)
    {
        Guid connectionId = reader.ReadGuid();
        int processId = reader.ReadInt32();

        int count = reader.ReadInt32();
        string[] capabilities = new string[count];

        for (int i = 0; i < count; i++)
        {
            capabilities[i] = reader.ReadString();
        }

        return new(connectionId, processId, ImmutableCollectionsMarshal.AsImmutableArray(capabilities));
    }
}
