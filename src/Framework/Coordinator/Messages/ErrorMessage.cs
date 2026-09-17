// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  An error from the coordinator.
/// </summary>
internal sealed record ErrorMessage : ServerMessage
{
    public string Message { get; }

    public ErrorMessage(string message)
        : base(ServerMessageType.Error)
    {
        Message = message;
    }

    protected override void WritePayload(BinaryWriter writer)
    {
        writer.Write(Message);
    }

    internal static ErrorMessage ReadPayload(BinaryReader reader)
    {
        string message = reader.ReadString();

        return new(message);
    }
}
