// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Wraps a task-scoped packet from a TaskHost (log message, callback request, yield request, or
/// completion) with the ID of the task that sent it. The worker node routes the inner packet to
/// that task's handler, so tasks that overlap in one TaskHost do not receive each other's packets.
/// </summary>
/// <remarks>
/// The inner packet is translated with the same translator as the envelope, so it keeps its own
/// version-dependent serialization.
/// </remarks>
internal sealed class TaskHostTaskEnvelope : INodePacket
{
    private int _taskId;
    private INodePacket _packet;

    public TaskHostTaskEnvelope(int taskId, INodePacket packet)
    {
        _taskId = taskId;
        _packet = packet;
    }

    public NodePacketType Type => NodePacketType.TaskHostTaskEnvelope;

    /// <summary>
    /// The worker node's ID for the task, from <see cref="TaskHostTaskIdentity"/>.
    /// </summary>
    public int TaskId => _taskId;

    /// <summary>
    /// The wrapped packet.
    /// </summary>
    public INodePacket Packet => _packet;

    public void Translate(ITranslator translator)
    {
        if (translator.Mode != TranslationDirection.WriteToStream)
        {
            throw new InvalidOperationException("Use FactoryForDeserialization to read a TaskHostTaskEnvelope.");
        }

        translator.Translate(ref _taskId);
        byte packetType = (byte)_packet.Type;
        translator.Translate(ref packetType);
        _packet.Translate(translator);
    }

    /// <summary>
    /// Reads an envelope. <paramref name="deserializeInner"/> creates the inner packet from its type.
    /// </summary>
    internal static INodePacket FactoryForDeserialization(ITranslator translator, Func<NodePacketType, ITranslator, INodePacket> deserializeInner)
    {
        int taskId = 0;
        translator.Translate(ref taskId);
        byte packetType = 0;
        translator.Translate(ref packetType);
        INodePacket packet = deserializeInner((NodePacketType)packetType, translator);
        return new TaskHostTaskEnvelope(taskId, packet);
    }
}
