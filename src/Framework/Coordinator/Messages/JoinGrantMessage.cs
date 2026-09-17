// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Request to join an existing coordinator grant without consuming additional global budget.
/// </summary>
internal sealed record JoinGrantMessage : ClientMessage
{
    public Guid GrantId { get; }

    public int RequestedNodes { get; }

    public JoinGrantMessage(Guid grantId, int requestedNodes)
        : base(ClientMessageType.JoinGrant)
    {
        GrantId = grantId;
        RequestedNodes = requestedNodes;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.WriteGuid(GrantId);
        writer.Write(RequestedNodes);
    }

    internal static JoinGrantMessage ReadPayload(BinaryReader reader)
    {
        Guid grantId = reader.ReadGuid();
        int requestedNodes = reader.ReadInt32();

        return new(grantId, requestedNodes);
    }
}
