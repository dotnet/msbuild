// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  A node grant from the coordinator with an associated grant token.
/// </summary>
internal sealed record NodeGrantWithIdMessage : ServerMessage, INodeGrantMessage
{
    public override ServerMessageType MessageType => ServerMessageType.NodeGrantWithId;

    public Guid GrantId { get; }

    public int GrantedNodes { get; }

    public NodeGrantWithIdMessage(Guid grantId, int grantedNodes)
    {
        GrantId = grantId;
        GrantedNodes = grantedNodes;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.WriteGuid(GrantId);
        writer.Write(GrantedNodes);
    }

    internal static NodeGrantWithIdMessage ReadPayload(BinaryReader reader)
        => new(grantId: reader.ReadGuid(), grantedNodes: reader.ReadInt32());
}
