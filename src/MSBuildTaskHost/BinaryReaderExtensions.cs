// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Shared
{
    internal static class BinaryReaderExtensions
    {
        public static string? ReadOptionalString(this BinaryReader reader)
        {
            return reader.ReadByte() == 0 ? null : reader.ReadString();
        }

        public static int? ReadOptionalInt32(this BinaryReader reader)
        {
            return reader.ReadByte() == 0 ? null : reader.ReadInt32();
        }

        public static int Read7BitEncodedInt(this BinaryReader reader)
        {
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                {
                    throw new FormatException();
                }

                // ReadByte handles end of stream cases for us.
                b = reader.ReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return count;
        }

        public static DateTime ReadTimestamp(this BinaryReader reader)
        {
            long timestampTicks = reader.ReadInt64();
            DateTimeKind kind = (DateTimeKind)reader.ReadInt32();
            var timestamp = new DateTime(timestampTicks, kind);
            return timestamp;
        }

        public static unsafe Guid ReadGuid(this BinaryReader reader)
        {
            return new Guid(reader.ReadBytes(sizeof(Guid)));
        }

        public static Dictionary<string, TimeSpan> ReadDurationDictionary(this BinaryReader reader)
        {
            int count = reader.Read7BitEncodedInt();
            var durations = new Dictionary<string, TimeSpan>(count);
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                TimeSpan value = TimeSpan.FromTicks(reader.ReadInt64());

                durations.Add(key, value);
            }

            return durations;
        }
    }
}
