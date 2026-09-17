// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

internal abstract partial record ServerMessage
{
    private sealed class Factory : FactoryBase<ServerMessage, Factory>
    {
        private static readonly Group s_allFactories = new(
            new(ServerMessageType.HandshakeResponse, static (reader, _) => ServerHandshakeMessage.ReadPayload(reader)),
            new(ServerMessageType.NodeGrant, static (reader, extendedFieldsByte)
                => NodeGrantMessage.ReadPayload(
                    reader,
                    (NodeGrantMessage.ExtendedFields)extendedFieldsByte),
                    (byte)NodeGrantMessage.ExtendedFields.GrantId),
            new(WaitMessage.Instance),
            new(ServerMessageType.Error, static (reader, _) => ErrorMessage.ReadPayload(reader)));

        private Factory(ServerMessage instance, byte supportedExtendedFields = 0)
            : base(instance.MessageType, instance, supportedExtendedFields)
        {
        }

        private Factory(ServerMessageType messageType, Func<BinaryReader, byte, ServerMessage> messageCreator, byte supportedExtendedFields = 0)
            : base(messageType, messageCreator, supportedExtendedFields)
        {
        }

        public static Factory FromMessageType(ServerMessageType messageType)
            => s_allFactories.GetFactory(messageType);
    }
}
