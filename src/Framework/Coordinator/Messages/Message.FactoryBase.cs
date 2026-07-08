// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

internal abstract partial record Message<TMessageType>
{
    protected abstract partial class FactoryBase<TMessage, TFactory>
       where TMessage : Message<TMessageType>
       where TFactory : FactoryBase<TMessage, TFactory>
    {
        public TMessageType MessageType { get; }

        private readonly TMessage? _instance;
        private readonly Func<BinaryReader, TMessage>? _messageCreator;

        protected FactoryBase(TMessageType messageType, Func<BinaryReader, TMessage> messageCreator)
        {
            MessageType = messageType;
            _instance = null;
            _messageCreator = messageCreator;
        }

        protected FactoryBase(TMessageType messageType, TMessage instance)
        {
            MessageType = messageType;
            _instance = instance;
            _messageCreator = null;
        }

        public TMessage Create(BinaryReader reader)
        {
            if (_instance is not null)
            {
                return _instance;
            }

            Assumed.NotNull(_messageCreator, "Message factory must have either an instance or a message creator.");

            return _messageCreator(reader);
        }
    }
}
