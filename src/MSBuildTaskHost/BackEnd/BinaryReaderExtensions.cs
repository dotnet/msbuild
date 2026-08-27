// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.TaskHost.BackEnd;

internal static class BinaryReaderExtensions
{
    public static string? ReadOptionalString(this BinaryReader reader)
        => reader.ReadByte() == 0 ? null : reader.ReadString();

    public static int? ReadOptionalInt32(this BinaryReader reader)
        => reader.ReadByte() == 0 ? null : reader.ReadInt32();
}
