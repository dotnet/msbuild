// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Base type for all messages sent from the coordinator to an MSBuild client.
/// </summary>
internal abstract partial record ServerMessage : Message<ServerMessageType>
{
    protected ServerMessage(ServerMessageType messageType)
        : base(messageType)
    {
    }

    public static ServerMessage ReadFrom(BinaryReader reader)
    {
        ServerMessageType messageType = ReadTypeByte(reader);
        Factory factory = Factory.FromMessageType(messageType);

        return factory.Create(reader);
    }
}
