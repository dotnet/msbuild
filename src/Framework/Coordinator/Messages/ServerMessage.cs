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
        (ServerMessageType messageType, bool hasExtendedFields) = ReadTypeByte(reader);
        Factory factory = Factory.FromMessageType(messageType);

        byte extendedFields = hasExtendedFields ? ReadExtendedFieldsByte(reader) : (byte)0;

        // The marker bit is only legal for message types that declare a supported extended-field mask.
        // The factory validates that the actual field bits are within that mask.
        Assumed.False(hasExtendedFields && !factory.SupportsExtendedFields, $"Message type {factory.MessageType} does not support extended fields.");

        return factory.Create(reader, extendedFields);
    }
}
