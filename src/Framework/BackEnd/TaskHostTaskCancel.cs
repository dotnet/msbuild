// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Cancels one task in a TaskHost. Unlike <see cref="TaskHostTaskCancelled"/>, which cancels every
/// task in the process, this does not affect other tasks that share the TaskHost.
/// </summary>
internal sealed class TaskHostTaskCancel : INodePacket
{
    private int _taskId;

    public TaskHostTaskCancel(int taskId)
    {
        _taskId = taskId;
    }

    public NodePacketType Type => NodePacketType.TaskHostTaskCancel;

    /// <summary>
    /// The worker node's ID for the task to cancel, from <see cref="TaskHostTaskIdentity"/>.
    /// </summary>
    public int TaskId => _taskId;

    public void Translate(ITranslator translator)
    {
        translator.Translate(ref _taskId);
    }

    internal static INodePacket FactoryForDeserialization(ITranslator translator)
    {
        TaskHostTaskCancel packet = new(0);
        packet.Translate(translator);
        return packet;
    }
}
