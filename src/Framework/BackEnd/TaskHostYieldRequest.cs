// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Requests Yield or Reacquire on the owning request's execution thread.
/// </summary>
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
