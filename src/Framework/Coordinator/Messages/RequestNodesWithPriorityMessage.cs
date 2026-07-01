// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Request a node grant from the coordinator with a queue scheduling priority.
/// </summary>
internal sealed record RequestNodesWithPriorityMessage : ClientMessage
{
    public int RequestedNodes { get; }

    public CoordinatorBuildPriority Priority { get; }

    public RequestNodesWithPriorityMessage(int requestedNodes, CoordinatorBuildPriority priority)
        : base(ClientMessageType.RequestNodesWithPriority)
    {
        if (priority is < CoordinatorBuildPriority.Low or > CoordinatorBuildPriority.High)
        {
            throw new ArgumentOutOfRangeException(nameof(priority), priority, $"Coordinator build priority must be {CoordinatorBuildPriority.Low}, {CoordinatorBuildPriority.Normal}, or {CoordinatorBuildPriority.High}.");
        }

        RequestedNodes = requestedNodes;
        Priority = priority;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.Write(RequestedNodes);
        writer.Write((int)Priority);
    }

    internal static RequestNodesWithPriorityMessage ReadPayload(BinaryReader reader)
    {
        int requestedNodes = reader.ReadInt32();
        int rawPriority = reader.ReadInt32();

        if (rawPriority is < (int)CoordinatorBuildPriority.Low or > (int)CoordinatorBuildPriority.High)
        {
            throw new InvalidDataException($"Unknown coordinator build priority value: {rawPriority}");
        }

        return new(requestedNodes, (CoordinatorBuildPriority)rawPriority);
    }
}
