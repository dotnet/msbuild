// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  A node grant from the coordinator.
/// </summary>
internal sealed record NodeGrantMessage : ServerMessage
{
    public override ServerMessageType MessageType => ServerMessageType.NodeGrant;

    public int GrantedNodes { get; }

    public NodeGrantMessage(int grantedNodes)
    {
        GrantedNodes = grantedNodes;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.Write(GrantedNodes);
    }
}
