// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

internal abstract partial record ClientMessage
{
    private sealed class Factory : FactoryBase<ClientMessage, Factory>
    {
        private static readonly Group s_allFactories = new(
            new(ClientMessageType.Handshake, static reader => ClientHandshakeMessage.ReadPayload(reader)),
            new(ClientMessageType.RequestNodes, static reader => RequestNodesMessage.ReadPayload(reader)),
            new(ReleaseNodesMessage.Instance),
            new(HeartbeatMessage.Instance),
            new(ClientMessageType.JoinGrant, static reader => JoinGrantMessage.ReadPayload(reader)));

        private Factory(ClientMessage instance)
            : base(instance.MessageType, instance)
        {
        }

        private Factory(ClientMessageType messageType, Func<BinaryReader, ClientMessage> messageCreator)
            : base(messageType, messageCreator)
        {
        }

        public static Factory FromMessageType(ClientMessageType messageType)
            => s_allFactories.GetFactory(messageType);
    }
}
