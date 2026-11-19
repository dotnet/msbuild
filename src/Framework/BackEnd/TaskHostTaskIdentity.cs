// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Sent by the worker node immediately before a <see cref="TaskHostConfiguration"/>. It gives the task
/// the ID that the worker node uses to route the task's packets, and says whether the task may yield.
/// </summary>
/// <remarks>
/// Sent only when the negotiated packet version is at least
/// <see cref="NodePacketTypeExtensions.TaskHostYieldMinVersion"/>. The payload does not depend on the
/// negotiated packet version, because CLR4 task hosts read packets from the worker node without a version.
/// </remarks>
internal sealed class TaskHostTaskIdentity : INodePacket
{
    private int _taskId;
    private bool _yieldEnabled;

    public TaskHostTaskIdentity(int taskId, bool yieldEnabled)
    {
        _taskId = taskId;
        _yieldEnabled = yieldEnabled;
    }

    public NodePacketType Type => NodePacketType.TaskHostTaskIdentity;

    /// <summary>
    /// The worker node's ID for the task. Never zero.
    /// </summary>
    public int TaskId => _taskId;

    /// <summary>
    /// Whether IBuildEngine3.Yield and Reacquire are forwarded to the worker node for this task.
    /// </summary>
    public bool YieldEnabled => _yieldEnabled;

    public void Translate(ITranslator translator)
    {
        translator.Translate(ref _taskId);
        translator.Translate(ref _yieldEnabled);
    }

    internal static INodePacket FactoryForDeserialization(ITranslator translator)
    {
        TaskHostTaskIdentity packet = new(0, false);
        packet.Translate(translator);
        return packet;
    }
}
