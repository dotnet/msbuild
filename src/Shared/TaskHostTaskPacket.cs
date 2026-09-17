// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Identifies the task invocation that owns a TaskHost message, independently of completion order.
/// Connection-level messages are never enclosed.
/// </summary>
internal sealed class TaskHostTaskPacket : INodePacket
{
    internal TaskHostTaskPacket(long invocationId, INodePacket packet)
    {
        if (invocationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(invocationId));
        }

        ArgumentNullException.ThrowIfNull(packet);
        ValidatePacketType(packet.Type);
        InvocationId = invocationId;
        Packet = packet;
    }

    internal long InvocationId { get; }

    internal INodePacket Packet { get; }

    public NodePacketType Type => NodePacketType.TaskHostTaskPacket;

    public void Translate(ITranslator translator)
    {
        if (translator.Mode != TranslationDirection.WriteToStream ||
            translator.NegotiatedPacketVersion is not >= NodePacketTypeExtensions.TaskHostInvocationMinVersion)
        {
            throw new InvalidDataException("Task invocation packets require a compatible TaskHost protocol.");
        }

        long invocationId = InvocationId;
        byte packetType = (byte)Packet.Type;
        translator.Translate(ref invocationId);
        translator.Translate(ref packetType);
        Packet.Translate(translator);
    }

    internal static INodePacket FactoryForDeserialization(ITranslator translator, INodePacketFactory factory)
    {
        if (translator.NegotiatedPacketVersion is not >= NodePacketTypeExtensions.TaskHostInvocationMinVersion)
        {
            throw new InvalidDataException("Unexpected task invocation packet on a legacy connection.");
        }

        long invocationId = 0;
        byte packetType = 0;
        translator.Translate(ref invocationId);
        translator.Translate(ref packetType);
        if (invocationId <= 0)
        {
            throw new InvalidDataException("Task invocation identity must be positive.");
        }

        ValidatePacketType((NodePacketType)packetType);
        return new TaskHostTaskPacket(invocationId, factory.DeserializePacket((NodePacketType)packetType, translator));
    }

    private static void ValidatePacketType(NodePacketType packetType)
    {
        if (packetType is not (NodePacketType.LogMessage or NodePacketType.TaskHostTaskComplete or
            NodePacketType.TaskHostBuildRequest or NodePacketType.TaskHostCoresRequest or
            NodePacketType.TaskHostIsRunningMultipleNodesRequest))
        {
            throw new InvalidDataException($"Packet {packetType} is not a task-originated message.");
        }
    }
}
