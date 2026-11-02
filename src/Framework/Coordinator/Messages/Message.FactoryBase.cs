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

        /// <summary>
        ///  Gets the extended field bits this message reader understands. Incoming messages that set bits outside
        ///  this mask are rejected before payload parsing so older readers do not silently skip unknown fields.
        /// </summary>
        public byte SupportedExtendedFields { get; }

        /// <summary>
        ///  Gets whether this message type may appear on the wire with the extended-fields marker set. This is
        ///  distinct from a particular message instance having non-zero extended fields.
        /// </summary>
        public bool SupportsExtendedFields => SupportedExtendedFields > 0;

        private readonly TMessage? _instance;
        private readonly Func<BinaryReader, byte, TMessage>? _messageCreator;

        protected FactoryBase(TMessageType messageType, Func<BinaryReader, byte, TMessage> messageCreator, byte supportedExtendedFields)
        {
            MessageType = messageType;
            _instance = null;
            _messageCreator = messageCreator;
            SupportedExtendedFields = supportedExtendedFields;
        }

        protected FactoryBase(TMessageType messageType, TMessage instance, byte supportedExtendedFields)
        {
            MessageType = messageType;
            _instance = instance;
            _messageCreator = null;
            SupportedExtendedFields = supportedExtendedFields;
        }

        public TMessage Create(BinaryReader reader, byte extendedFields)
        {
            Assumed.Zero(extendedFields & ~SupportedExtendedFields, $"Message type {MessageType} contains unsupported extended fields.");

            if (_instance is not null)
            {
                Assumed.Zero(extendedFields, "Instance-based message factories cannot consume extended fields.");
                return _instance;
            }

            Assumed.NotNull(_messageCreator, "Message factory must have either an instance or a message creator.");

            return _messageCreator(reader, extendedFields);
        }
    }
}
