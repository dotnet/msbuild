// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.BackEnd;

/// <summary>
/// Response from the worker node after Yield or Reacquire completed in its scheduler.
/// A Reacquire response is sent only after the owning request is active again, or with
/// the exception that stopped reacquisition (for example, when the build was aborted).
/// </summary>
internal sealed class TaskHostYieldResponse : INodePacket, ITaskHostCallbackPacket
{
    private int _requestId;
    private Exception? _exception;

    public TaskHostYieldResponse(int requestId, Exception? exception = null)
    {
        _requestId = requestId;
        _exception = exception;
    }

    public NodePacketType Type => NodePacketType.TaskHostYieldResponse;

    public int RequestId
    {
        get => _requestId;
        set => _requestId = value;
    }

    public Exception? Exception => _exception;

    public void Translate(ITranslator translator)
    {
        translator.Translate(ref _requestId);
        translator.TranslateException(ref _exception);
    }

    internal static INodePacket FactoryForDeserialization(ITranslator translator)
    {
        TaskHostYieldResponse packet = new(0);
        packet.Translate(translator);
        return packet;
    }
}
