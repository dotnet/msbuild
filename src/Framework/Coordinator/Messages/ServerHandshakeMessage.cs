// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Handshake response sent by the server after receiving a <see cref="ClientHandshakeMessage"/>.
///  Advertises the server's capabilities.
/// </summary>
internal sealed record ServerHandshakeMessage : ServerMessage
{
    /// <summary>
    ///  Gets the capabilities advertised by the server.
    /// </summary>
    public ImmutableArray<string> Capabilities { get; }

    public ServerHandshakeMessage(ImmutableArray<string> capabilities)
        : base(ServerMessageType.HandshakeResponse)
    {
        Capabilities = capabilities.IsDefault ? [] : capabilities;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.Write(Capabilities.Length);

        foreach (string capability in Capabilities)
        {
            writer.Write(capability);
        }
    }

    internal static ServerHandshakeMessage ReadPayload(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        string[] capabilities = new string[count];

        for (int i = 0; i < count; i++)
        {
            capabilities[i] = reader.ReadString();
        }

        return new(ImmutableCollectionsMarshal.AsImmutableArray(capabilities));
    }
}
