// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Request from a TaskHost task to Yield or Reacquire its owning request on the worker node.
/// </summary>
/// <remarks>
/// Sent inside a <see cref="TaskHostTaskEnvelope"/>, so the worker node routes it to the task that yields.
/// The payload does not depend on the negotiated packet version.
/// </remarks>
internal sealed class TaskHostYieldRequest : INodePacket, ITaskHostCallbackPacket
{
    private int _requestId;
    private bool _reacquire;

    public TaskHostYieldRequest(bool reacquire)
    {
        _reacquire = reacquire;
    }

    public NodePacketType Type => NodePacketType.TaskHostYieldRequest;

    public int RequestId
    {
        get => _requestId;
        set => _requestId = value;
    }

    /// <summary>
    /// True for Reacquire; false for Yield.
    /// </summary>
    public bool Reacquire => _reacquire;

    public void Translate(ITranslator translator)
    {
        translator.Translate(ref _requestId);
        translator.Translate(ref _reacquire);
    }

    internal static INodePacket FactoryForDeserialization(ITranslator translator)
    {
        TaskHostYieldRequest packet = new(false);
        packet.Translate(translator);
        return packet;
    }
}
