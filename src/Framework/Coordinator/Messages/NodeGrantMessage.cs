// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  A node grant from the coordinator.
/// </summary>
internal sealed partial record NodeGrantMessage : ServerMessage
{
    private readonly ExtendedFields _extendedFields;

    protected override byte ExtendedFieldsByte => (byte)_extendedFields;

    /// <summary>
    ///  The root grant token that nested clients can use to join this grant, or <see cref="Guid.Empty"/> if
    ///  <see cref="ExtendedFields.GrantId"/> is not set.
    /// </summary>
    public Guid GrantId { get; }

    public int GrantedNodes { get; }

    public NodeGrantMessage(int grantedNodes)
        : this(grantId: Guid.Empty, grantedNodes, ExtendedFields.None)
    {
    }

    public NodeGrantMessage(Guid grantId, int grantedNodes)
        : this(grantId, grantedNodes, ExtendedFields.GrantId)
    {
    }

    private NodeGrantMessage(Guid grantId, int grantedNodes, ExtendedFields extendedFields)
        : base(ServerMessageType.NodeGrant)
    {
        Assumed.True(
            (extendedFields & ExtendedFields.GrantId) != 0 || grantId == Guid.Empty,
            $"{nameof(grantId)} must be empty if {nameof(ExtendedFields)}.{nameof(ExtendedFields.GrantId)} is not set.");

        GrantId = grantId;
        GrantedNodes = grantedNodes;
        _extendedFields = extendedFields;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        if ((_extendedFields & ExtendedFields.GrantId) != 0)
        {
            writer.WriteGuid(GrantId);
        }

        writer.Write(GrantedNodes);
    }

    internal static NodeGrantMessage ReadPayload(BinaryReader reader, ExtendedFields extendedFields)
    {
        Guid grantId = (extendedFields & ExtendedFields.GrantId) != 0
            ? reader.ReadGuid()
            : Guid.Empty;

        int grantedNodes = reader.ReadInt32();

        return new(grantId, grantedNodes, extendedFields);
    }
}
