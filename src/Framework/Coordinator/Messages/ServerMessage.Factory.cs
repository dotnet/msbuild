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
            new(ServerMessageType.HandshakeResponse, static reader => ServerHandshakeMessage.ReadPayload(reader)),
            new(ServerMessageType.NodeGrant, static reader => NodeGrantMessage.ReadPayload(reader)),
            new(WaitMessage.Instance),
            new(ServerMessageType.Error, static reader => ErrorMessage.ReadPayload(reader)),
            new(ServerMessageType.NodeGrantWithId, static reader => NodeGrantWithIdMessage.ReadPayload(reader)));

        private Factory(ServerMessage instance)
            : base(instance.MessageType, instance)
        {
        }

        private Factory(ServerMessageType messageType, Func<BinaryReader, ServerMessage> messageCreator)
            : base(messageType, messageCreator)
        {
        }

        public static Factory FromMessageType(ServerMessageType messageType)
            => s_allFactories.GetFactory(messageType);
    }
}
