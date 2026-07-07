// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.TaskHost.BackEnd;

internal static class BinaryWriterExtensions
{
    public static void WriteOptionalString(this BinaryWriter writer, string? value)
    {
        if (value == null)
        {
            writer.Write((byte)0);
        }
        else
        {
            writer.Write((byte)1);
            writer.Write(value);
        }
    }

    public static void WriteOptionalInt32(this BinaryWriter writer, int? value)
    {
        if (value == null)
        {
            writer.Write((byte)0);
        }
        else
        {
            writer.Write((byte)1);
            writer.Write(value.Value);
        }
    }
}
