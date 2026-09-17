// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Base type shared by <see cref="ClientMessage"/> and <see cref="ServerMessage"/>, holding the wire framing
///  logic common to both directions of the coordinator protocol: a single type byte, optionally followed by a
///  single extended fields byte when <see cref="ExtendedFieldsByte"/> is non-zero, followed by the message's
///  own payload.
/// </summary>
/// <typeparam name="TMessageType">
///  The enum type that identifies message kinds on the wire. The enum must have <see cref="byte"/> as its
///  underlying type.
/// </typeparam>
internal abstract partial record Message<TMessageType>
    where TMessageType : struct, Enum
{
    private const byte ExtendedBit = 0x80;

    static Message()
    {
        Assumed.Equal(
            Enum.GetUnderlyingType(typeof(TMessageType)), typeof(byte),
            $"{nameof(TMessageType)} must have an underlying type of byte.");
    }

    private readonly TMessageType _messageType;

    /// <summary>
    ///  Capability-gated field flags for this specific message instance. A value of <c>0</c> omits the extended
    ///  fields byte from the wire format; non-zero values cause the byte to be emitted and interpreted by the
    ///  receiving message type.
    /// </summary>
    protected virtual byte ExtendedFieldsByte => 0;

    public TMessageType MessageType => _messageType;

    protected Message(TMessageType messageType)
    {
        _messageType = messageType;
    }

    private static byte GetTypeByte(TMessageType messageType)
       => Unsafe.As<TMessageType, byte>(ref messageType);

    private static TMessageType GetMessageType(byte typeByte)
        => Unsafe.As<byte, TMessageType>(ref typeByte);

    public void WriteTo(BinaryWriter writer)
    {
        byte typeByte = GetTypeByte(_messageType);
        byte extendedFieldsByte = ExtendedFieldsByte;

        if (extendedFieldsByte != 0)
        {
            // UNDONE: The extended fields byte reserves the extended bit for future expansion via more than one byte of extended fields.
            // For now, we don't support that, so assert that the extended bit is not set.
            Assumed.Zero(extendedFieldsByte & ExtendedBit, "Extended fields byte must not set the extended bit.");

            writer.Write((byte)(typeByte | ExtendedBit));
            writer.Write(extendedFieldsByte);
        }
        else
        {
            writer.Write(typeByte);
        }

        WritePayload(writer);
        writer.Flush();
    }

    protected virtual void WritePayload(BinaryWriter writer)
    {
        // Descendants can override.
    }

    /// <summary>
    ///  Reads the wire type byte and splits it into the type ordinal (with the extended-fields bit masked off)
    ///  and whether an extended fields byte follows.
    /// </summary>
    protected static (TMessageType MessageType, bool HasExtendedFields) ReadTypeByte(BinaryReader reader)
    {
        byte typeByte = reader.ReadByte();
        bool hasExtendedFields = false;

        if ((typeByte & ExtendedBit) != 0)
        {
            hasExtendedFields = true;

            // Strip the extended-fields bit off the type byte before casting it to TMessageType.
            typeByte = (byte)(typeByte & ~ExtendedBit);
        }

        TMessageType messageType = GetMessageType(typeByte);

        return (messageType, hasExtendedFields);
    }

    /// <summary>
    ///  Reads the single extended fields byte for a message type that supports it.
    /// </summary>
    protected static byte ReadExtendedFieldsByte(BinaryReader reader)
    {
        byte extendedFieldsByte = reader.ReadByte();

        // UNDONE: The extended fields byte reserves the extended bit for future expansion via more than one byte of extended fields.
        // For now, we don't support that, so assert that the extended bit is not set.
        Assumed.Zero(extendedFieldsByte & ExtendedBit, "Multi-byte extended fields are not yet supported.");

        return extendedFieldsByte;
    }
}
