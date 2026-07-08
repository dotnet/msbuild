// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Base type for all messages sent from an MSBuild client to the coordinator.
/// </summary>
internal abstract partial record ClientMessage : Message<ClientMessageType>
{
    protected ClientMessage(ClientMessageType messageType)
        : base(messageType)
    {
    }

    public static ClientMessage ReadFrom(BinaryReader reader)
    {
        ClientMessageType messageType = ReadTypeByte(reader);
        Factory factory = Factory.FromMessageType(messageType);

        return factory.Create(reader);
    }
}
