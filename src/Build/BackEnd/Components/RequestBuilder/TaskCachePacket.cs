// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.BackEnd;

internal enum TaskCacheOperation
{
    ReadManifest,
    Open,
    PutFile,
    Publish,
}

/// <summary>Storage-only RPC carried by the existing, version-negotiated worker connection.</summary>
internal sealed class TaskCachePacket(NodePacketType type) : INodePacket
{
    internal int Id;
    internal int Operation;
    internal string Key = String.Empty;
    internal string? Path;
    internal byte[]? Data;
    internal string[]? Artifacts;
    internal int MaximumSize;
    internal string? Error;
    internal bool Cancelled;
    internal bool Published;

    public NodePacketType Type { get; } = type;

    public void Translate(ITranslator translator)
    {
        translator.Translate(ref Id);
        translator.Translate(ref Operation);
        translator.Translate(ref Key);
        translator.Translate(ref Path);
        translator.Translate(ref Data);
        translator.Translate(ref Artifacts);
        translator.Translate(ref MaximumSize);
        translator.Translate(ref Error);
        translator.Translate(ref Cancelled);
        translator.Translate(ref Published);
    }

    internal static INodePacket ReadRequest(ITranslator translator) => Read(NodePacketType.TaskCacheRequest, translator);
    internal static INodePacket ReadResponse(ITranslator translator) => Read(NodePacketType.TaskCacheResponse, translator);
    internal static INodePacket ReadCancel(ITranslator translator) => Read(NodePacketType.TaskCacheCancel, translator);

    private static TaskCachePacket Read(NodePacketType type, ITranslator translator)
    {
        TaskCachePacket packet = new(type);
        packet.Translate(translator);
        return packet;
    }
}
