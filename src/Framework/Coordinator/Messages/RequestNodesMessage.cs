// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Request a node grant from the coordinator.
/// </summary>
internal sealed partial record RequestNodesMessage : ClientMessage
{
    private readonly ExtendedFields _extendedFields;

    protected override byte ExtendedFieldsByte => (byte)_extendedFields;

    public int RequestedNodes { get; }

    public CoordinatorBuildPriority Priority { get; }

    public RequestNodesMessage(int requestedNodes)
        : this(requestedNodes, CoordinatorBuildPriority.Normal, ExtendedFields.None)
    {
    }

    public RequestNodesMessage(int requestedNodes, CoordinatorBuildPriority priority)
        : this(requestedNodes, priority, ExtendedFields.Priority)
    {
    }

    private RequestNodesMessage(int requestedNodes, CoordinatorBuildPriority priority, ExtendedFields extendedFields)
        : base(ClientMessageType.RequestNodes)
    {
        if (priority is < CoordinatorBuildPriority.Low or > CoordinatorBuildPriority.High)
        {
            throw new ArgumentOutOfRangeException(nameof(priority), priority, $"Coordinator build priority must be {CoordinatorBuildPriority.Low}, {CoordinatorBuildPriority.Normal}, or {CoordinatorBuildPriority.High}.");
        }

        Assumed.True(
            (extendedFields & ExtendedFields.Priority) != 0 || priority == CoordinatorBuildPriority.Normal,
            $"{nameof(priority)} must be {CoordinatorBuildPriority.Normal} if {nameof(ExtendedFields)}.{nameof(ExtendedFields.Priority)} is not set.");

        RequestedNodes = requestedNodes;
        Priority = priority;
        _extendedFields = extendedFields;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        if ((_extendedFields & ExtendedFields.Priority) != 0)
        {
            writer.Write((int)Priority);
        }

        writer.Write(RequestedNodes);
    }

    internal static RequestNodesMessage ReadPayload(BinaryReader reader, ExtendedFields extendedFields)
    {
        CoordinatorBuildPriority priority = (extendedFields & ExtendedFields.Priority) != 0
            ? ReadPriority(reader)
            : CoordinatorBuildPriority.Normal;

        int requestedNodes = reader.ReadInt32();

        return new(requestedNodes, priority, extendedFields);
    }

    private static CoordinatorBuildPriority ReadPriority(BinaryReader reader)
    {
        int rawPriority = reader.ReadInt32();

        if (rawPriority is < (int)CoordinatorBuildPriority.Low or > (int)CoordinatorBuildPriority.High)
        {
            throw new InvalidDataException($"Unknown coordinator build priority value: {rawPriority}");
        }

        return (CoordinatorBuildPriority)rawPriority;
    }
}
