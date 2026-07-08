// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.Framework.Coordinator;

/// <summary>
///  Base type shared by <see cref="ClientMessage"/> and <see cref="ServerMessage"/>, holding the wire framing
///  logic common to both directions of the coordinator protocol.
/// </summary>
/// <typeparam name="TMessageType">
///  The enum type that identifies message kinds on the wire. The enum must have <see cref="byte"/> as its
///  underlying type.
/// </typeparam>
internal abstract partial record Message<TMessageType>
    where TMessageType : struct, Enum
{
    static Message()
    {
        Assumed.Equal(
            Enum.GetUnderlyingType(typeof(TMessageType)), typeof(byte),
            $"{nameof(TMessageType)} must have an underlying type of byte.");
    }

    private readonly TMessageType _messageType;

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

        writer.Write(typeByte);
        WritePayload(writer);
        writer.Flush();
    }

    protected virtual void WritePayload(BinaryWriter writer)
    {
        // Descendants can override.
    }

    protected static TMessageType ReadTypeByte(BinaryReader reader)
    {
        byte typeByte = reader.ReadByte();
        TMessageType messageType = GetMessageType(typeByte);

        return messageType;
    }
}
