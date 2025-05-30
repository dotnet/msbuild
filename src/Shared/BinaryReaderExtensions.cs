// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared
{
    internal static class BinaryReaderExtensions
    {
#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static string? ReadOptionalString(this BinaryReader reader)
        {
            return reader.ReadByte() == 0 ? null : reader.ReadString();
        }

#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static int? ReadOptionalInt32(this BinaryReader reader)
        {
            return reader.ReadByte() == 0 ? null : reader.ReadInt32();
        }

#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
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

#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static DateTime ReadTimestamp(this BinaryReader reader)
        {
            long timestampTicks = reader.ReadInt64();
            DateTimeKind kind = (DateTimeKind)reader.ReadInt32();
            var timestamp = new DateTime(timestampTicks, kind);
            return timestamp;
        }

#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BuildEventContext? ReadOptionalBuildEventContext(this BinaryReader reader)
        {
            if (reader.ReadByte() == 0)
            {
                return null;
            }

            return reader.ReadBuildEventContext();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BuildEventContext ReadBuildEventContext(this BinaryReader reader)
        {
            int nodeId = reader.ReadInt32();
            int projectContextId = reader.ReadInt32();
            int targetId = reader.ReadInt32();
            int taskId = reader.ReadInt32();
            int submissionId = reader.ReadInt32();
            int projectInstanceId = reader.ReadInt32();
            int evaluationId = reader.ReadInt32();

            var buildEventContext = new BuildEventContext(submissionId, nodeId, evaluationId, projectInstanceId, projectContextId, targetId, taskId);
            return buildEventContext;
        }
#endif

#if !TASKHOST
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static unsafe Guid ReadGuid(this BinaryReader reader)
        {
            return new Guid(reader.ReadBytes(sizeof(Guid)));
        }

        public static void ReadExtendedBuildEventData(this BinaryReader reader, IExtendedBuildEventArgs data)
        {
            data.ExtendedType = reader.ReadString();
            data.ExtendedData = reader.ReadOptionalString();

            bool haveMetadata = reader.ReadBoolean();
            if (haveMetadata)
            {
                data.ExtendedMetadata = new Dictionary<string, string?>();

                int count = reader.Read7BitEncodedInt();
                for (int i = 0; i < count; i++)
                {
                    string key = reader.ReadString();
                    string? value = reader.ReadOptionalString();

                    data.ExtendedMetadata.Add(key, value);
                }
            }
            else
            {
                data.ExtendedMetadata = null;
            }
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
