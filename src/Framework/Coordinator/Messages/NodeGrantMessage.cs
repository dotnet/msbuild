// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  A node grant from the coordinator.
/// </summary>
internal sealed record NodeGrantMessage : ServerMessage, INodeGrantMessage
{
    public override ServerMessageType MessageType => ServerMessageType.NodeGrant;

    Guid INodeGrantMessage.GrantId => Guid.Empty;

    public int GrantedNodes { get; }

    public NodeGrantMessage(int grantedNodes)
    {
        GrantedNodes = grantedNodes;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.Write(GrantedNodes);
    }

    internal static NodeGrantMessage ReadPayload(BinaryReader reader)
    {
        int grantedNodes = reader.ReadInt32();

        return new(grantedNodes);
    }
}
