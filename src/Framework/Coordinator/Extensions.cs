// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework.Coordinator;

internal static class Extensions
{
    public static ClientMessage ReadClientMessage(this BinaryReader reader)
        => ClientMessage.ReadFrom(reader);

    public static ServerMessage ReadServerMessage(this BinaryReader reader)
        => ServerMessage.ReadFrom(reader);

    public static void Write(this BinaryWriter writer, ClientMessage message)
        => message.WriteTo(writer);

    public static void Write(this BinaryWriter writer, ServerMessage message)
        => message.WriteTo(writer);
}
